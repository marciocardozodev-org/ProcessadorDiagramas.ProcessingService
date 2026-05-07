#!/usr/bin/env bash
# test-lambda-local.sh
# Sobe a stack Lambda local e executa um fluxo E2E completo:
#   1. Baixa o Lambda RIE se necessário
#   2. Sobe postgres + localstack + migrate + Lambda (via RIE)
#   3. Faz upload do diagrama para S3 (LocalStack)
#   4. Invoca a Lambda via RIE com payload SQS
#   5. Valida resposta (nenhum BatchItemFailure)
#   6. Consome o resultado da fila SNS→SQS de resultados
#   7. Valida DB (job status = Completed)
#   8. Derruba a stack

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/docker-compose.lambda.yml"
RIE_DIR="$HOME/.aws-lambda-rie"
RIE_PATH="$RIE_DIR/aws-lambda-rie"
RIE_URL="https://github.com/aws/aws-lambda-runtime-interface-emulator/releases/latest/download/aws-lambda-rie-x86_64"

LOCALSTACK_PORT="4567"
LAMBDA_PORT="9000"
POSTGRES_PORT="5435"
POSTGRES_CONN="postgresql://postgres:postgres@localhost:$POSTGRES_PORT/processador_diagramas_processing"

PASS=0
FAIL=0

color_green="\033[0;32m"
color_red="\033[0;31m"
color_reset="\033[0m"

pass() { echo -e "${color_green}[PASS]${color_reset} $1"; PASS=$((PASS + 1)); }
fail() { echo -e "${color_red}[FAIL]${color_reset} $1"; FAIL=$((FAIL + 1)); }
info() { echo -e "       $1"; }

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "[ERROR] Comando não encontrado: $1"
    exit 1
  fi
}

cleanup() {
  echo ""
  echo "=== Derrubando stack ==="
  export RIE_PATH
  docker compose -f "$COMPOSE_FILE" down -v --remove-orphans 2>/dev/null || true
}

# ─── Dependências ────────────────────────────────────────────────────────────
require_cmd docker
require_cmd curl
require_cmd jq
require_cmd aws
require_cmd psql

# ─── Lambda RIE ──────────────────────────────────────────────────────────────
if [[ ! -f "$RIE_PATH" ]]; then
  echo "=== Baixando Lambda RIE ==="
  mkdir -p "$RIE_DIR"
  curl -fsSL "$RIE_URL" -o "$RIE_PATH"
  chmod +x "$RIE_PATH"
  echo "RIE baixado em $RIE_PATH"
fi

# ─── Variáveis LocalStack ─────────────────────────────────────────────────────
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_DEFAULT_REGION=us-east-1
export AWS_ENDPOINT_URL="http://localhost:$LOCALSTACK_PORT"
export RIE_PATH

# ─── Sobe stack ───────────────────────────────────────────────────────────────
trap cleanup EXIT

echo "=== Construindo e subindo stack Lambda local ==="
docker compose -f "$COMPOSE_FILE" build
docker compose -f "$COMPOSE_FILE" up -d

# ─── Aguarda Lambda RIE ───────────────────────────────────────────────────────
echo "=== Aguardando Lambda RIE ficar pronta ==="
LAMBDA_URL="http://localhost:$LAMBDA_PORT/2015-03-31/functions/function/invocations"
retries=60
for i in $(seq 1 $retries); do
  if curl -fsS --max-time 2 -X POST "$LAMBDA_URL" -d '{}' >/dev/null 2>&1; then
    echo "Lambda RIE pronta após ${i}s"
    break
  fi
  if [[ "$i" -eq "$retries" ]]; then
    echo "[ERROR] Timeout aguardando Lambda RIE"
    exit 1
  fi
  sleep 1
done

# ─── Prepara evento de teste ──────────────────────────────────────────────────
ANALYSIS_ID="$(cat /proc/sys/kernel/random/uuid)"
CORRELATION_ID="local-lambda-$(date +%s)"
REQUESTED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
BUCKET="lambda-input-diagrams"
KEY="input-diagrams/$ANALYSIS_ID/diagram.mmd"

echo "=== Fazendo upload do diagrama para S3 (LocalStack) ==="
DIAGRAM_CONTENT='graph TD
  A[Cliente] --> B[Gateway]
  B --> C[SQS]
  C --> D[Lambda]
  D --> E[Resultado]'

echo "$DIAGRAM_CONTENT" | aws s3 cp - "s3://$BUCKET/$KEY" \
  --endpoint-url "$AWS_ENDPOINT_URL" \
  --region us-east-1

# ─── Monta payload SQS (formato SNS-wrapped) ──────────────────────────────────
# A Lambda recebe um SQSEvent onde cada record.Body é a notificação SNS
INNER_PAYLOAD=$(jq -nc \
  --arg id "$ANALYSIS_ID" \
  --arg key "$KEY" \
  --arg cid "$CORRELATION_ID" \
  --arg ts "$REQUESTED_AT" \
  '{DiagramAnalysisProcessId:$id,InputStorageKey:$key,CorrelationId:$cid,RequestedAt:$ts}')

SNS_BODY=$(jq -nc \
  --arg msg "$INNER_PAYLOAD" \
  '{
    Type: "Notification",
    MessageId: "local-test-msg",
    Message: $msg,
    MessageAttributes: {
      eventType: { Type: "String", Value: "AnalysisProcessRequestedEvent" }
    }
  }')

MESSAGE_ID="local-$(date +%s%N)"

SQS_EVENT=$(jq -nc \
  --arg mid "$MESSAGE_ID" \
  --arg body "$SNS_BODY" \
  '{
    Records: [{
      messageId: $mid,
      receiptHandle: "local-receipt-handle",
      body: $body,
      attributes: {},
      messageAttributes: {},
      md5OfBody: "local",
      eventSource: "aws:sqs",
      eventSourceARN: "arn:aws:sqs:us-east-1:000000000000:lambda-input",
      awsRegion: "us-east-1"
    }]
  }')

# ─── Invoca Lambda ────────────────────────────────────────────────────────────
echo "=== Invocando Lambda via RIE ==="
echo "    AnalysisId: $ANALYSIS_ID"
echo "    CorrelationId: $CORRELATION_ID"

RESPONSE=$(curl -fsS -X POST "$LAMBDA_URL" \
  -H "Content-Type: application/json" \
  -d "$SQS_EVENT")

echo "    Resposta: $RESPONSE"

# Valida que não houve batch item failures
FAILURES=$(echo "$RESPONSE" | jq '.batchItemFailures | length // 0')
if [[ "$FAILURES" -eq 0 ]]; then
  pass "Lambda retornou sem BatchItemFailures"
else
  fail "Lambda retornou $FAILURES BatchItemFailures"
  echo "$RESPONSE" | jq '.batchItemFailures'
fi

# ─── Consome fila de resultados ────────────────────────────────────────────────
echo "=== Aguardando resultado na fila de resultados ==="
RESULTS_QUEUE_URL="http://localhost:$LOCALSTACK_PORT/000000000000/lambda-results"
FOUND_RESULT="false"
RESULT_MSG=""

for i in $(seq 1 30); do
  resp=$(aws sqs receive-message \
    --queue-url "$RESULTS_QUEUE_URL" \
    --max-number-of-messages 10 \
    --wait-time-seconds 2 \
    --endpoint-url "$AWS_ENDPOINT_URL" \
    --region us-east-1 2>/dev/null || echo '{}')

  count=$(echo "$resp" | jq '.Messages | length // 0')
  if [[ "$count" -gt 0 ]]; then
    while IFS= read -r msg; do
      body=$(echo "$msg" | jq -r '.Body')
      sns_msg=$(echo "$body" | jq -r '.Message // empty' 2>/dev/null || echo "")
      if [[ -z "$sns_msg" ]]; then continue; fi

      cid_out=$(echo "$sns_msg" | jq -r '.CorrelationId // empty' 2>/dev/null || echo "")
      if [[ "$cid_out" == "$CORRELATION_ID" ]]; then
        RESULT_MSG="$sns_msg"
        FOUND_RESULT="true"
        break 2
      fi
    done < <(echo "$resp" | jq -c '.Messages[]')
  fi

  sleep 1
done

if [[ "$FOUND_RESULT" == "true" ]]; then
  pass "Resultado encontrado na fila de saída"
  info "Payload: $(echo "$RESULT_MSG" | jq -c '.')"
else
  fail "Timeout aguardando resultado na fila de saída"
fi

# ─── Valida DB ────────────────────────────────────────────────────────────────
echo "=== Validando banco de dados ==="
DB_RESULT=$(psql "$POSTGRES_CONN" -At \
  -c "SELECT j.\"Status\", COUNT(r.\"Id\") FROM \"DiagramProcessingJobs\" j LEFT JOIN \"DiagramProcessingResults\" r ON r.\"DiagramProcessingJobId\" = j.\"Id\" WHERE j.\"DiagramAnalysisProcessId\" = '$ANALYSIS_ID' GROUP BY j.\"Status\";" \
  2>/dev/null || echo "")

if echo "$DB_RESULT" | grep -q "Completed"; then
  pass "Job no DB com status Completed"
else
  fail "Job não encontrado ou status incorreto no DB"
  info "Resultado: $DB_RESULT"
fi

# ─── Resumo ───────────────────────────────────────────────────────────────────
echo ""
echo "================================"
echo " Resultado: PASS=$PASS  FAIL=$FAIL"
echo "================================"

[[ "$FAIL" -eq 0 ]]
