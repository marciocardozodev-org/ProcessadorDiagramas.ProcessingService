#!/bin/bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INPUT_DIR="$ROOT_DIR/tmp/inputs"
INPUT_FILE_HOST="$INPUT_DIR/diagram-test.mmd"
INPUT_FILE_CONTAINER="/tmp/processing-inputs/diagram-test.mmd"
REQUEST_ID="$(cat /proc/sys/kernel/random/uuid)"
CORRELATION_ID="corr-$(date +%s)"
PASS=0
FAIL=0

require_cmd() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "[ERROR] Comando obrigatório não encontrado: $cmd"
    exit 1
  fi
}

assert_contains() {
  local description="$1"
  local haystack="$2"
  local needle="$3"

  if echo "$haystack" | grep -q "$needle"; then
    echo "[PASS] $description"
    PASS=$((PASS + 1))
  else
    echo "[FAIL] $description"
    echo "       Esperado conter: $needle"
    FAIL=$((FAIL + 1))
  fi
}

assert_http_ok() {
  local description="$1"
  local url="$2"

  local status
  status=$(curl -o /dev/null -s -w "%{http_code}" "$url" || echo "000")
  if [[ "$status" == "200" ]]; then
    echo "[PASS] $description (HTTP $status)"
    PASS=$((PASS + 1))
  else
    echo "[FAIL] $description (HTTP $status)"
    FAIL=$((FAIL + 1))
  fi
}

wait_for_http() {
  local url="$1"
  local retries=60
  local attempt=1

  until curl -fsS "$url" >/dev/null 2>&1; do
    if [[ "$attempt" -ge "$retries" ]]; then
      echo "[ERROR] Timeout aguardando endpoint $url"
      return 1
    fi

    attempt=$((attempt + 1))
    sleep 2
  done
}

receive_messages() {
  docker exec processador_diagramas_processing_service_localstack awslocal sqs receive-message \
    --queue-url http://localhost:4566/000000000000/analysis-processing-results \
    --message-attribute-names All \
    --max-number-of-messages 10 \
    --visibility-timeout 1 2>/dev/null || true
}

wait_for_result_message() {
  local retries=60
  local attempt=1

  while true; do
    local output
    output=$(receive_messages)

    if echo "$output" | grep -q "$REQUEST_ID"; then
      echo "$output"
      return 0
    fi

    if [[ "$attempt" -ge "$retries" ]]; then
      echo "[ERROR] Timeout aguardando mensagem de resultado para $REQUEST_ID"
      return 1
    fi

    attempt=$((attempt + 1))
    sleep 2
  done
}

query_postgres() {
  local sql="$1"
  docker exec processador_diagramas_processing_service_postgres \
    psql -U postgres -d processador_diagramas_processing -t -c "$sql" 2>/dev/null | tr -d ' '
}

send_analysis_request() {
  local request_id="$1"
  docker exec processador_diagramas_processing_service_localstack awslocal sqs send-message \
    --queue-url http://localhost:4566/000000000000/analysis-process-requests \
    --message-body "{\"DiagramAnalysisProcessId\":\"$request_id\",\"InputStorageKey\":\"$INPUT_FILE_CONTAINER\",\"CorrelationId\":\"$CORRELATION_ID\",\"RequestedAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}" \
    --message-attributes '{"eventType":{"StringValue":"AnalysisProcessRequestedEvent","DataType":"String"}}' >/dev/null
}

require_cmd docker
require_cmd curl
require_cmd grep

mkdir -p "$INPUT_DIR"
cat > "$INPUT_FILE_HOST" <<'EOF'
graph TD
  Gateway --> Processing
  Processing --> Reports
EOF

# ---------------------------------------------------------------------------
echo ""
echo "=== [1/5] Ambiente ==="
cd "$ROOT_DIR"
docker compose up -d --build postgres localstack migrate api

echo "[INFO] Aguardando health da API..."
wait_for_http "http://127.0.0.1:5080/health"

# ---------------------------------------------------------------------------
echo ""
echo "=== [2/5] Endpoints da API ==="
assert_http_ok "GET / retorna 200"    "http://127.0.0.1:5080/"
assert_http_ok "GET /health retorna 200" "http://127.0.0.1:5080/health"
assert_http_ok "GET /ready retorna 200"  "http://127.0.0.1:5080/ready"

ROOT_BODY=$(curl -s "http://127.0.0.1:5080/")
assert_contains "/ contém nome do serviço" "$ROOT_BODY" "ProcessadorDiagramas.ProcessingService"
assert_contains "/ contém role do worker"  "$ROOT_BODY" "processing-worker"

# ---------------------------------------------------------------------------
echo ""
echo "=== [3/5] Fluxo principal: mensagem → processamento → eventos ==="
echo "[INFO] Publicando AnalysisProcessRequestedEvent..."
send_analysis_request "$REQUEST_ID"

echo "[INFO] Aguardando eventos de saída..."
RESULT_MESSAGE="$(wait_for_result_message)"

assert_contains "AnalysisProcessingStartedEvent publicado"    "$RESULT_MESSAGE" "AnalysisProcessingStartedEvent"
assert_contains "AnalysisProcessingCompletedEvent publicado"  "$RESULT_MESSAGE" "AnalysisProcessingCompletedEvent"
assert_contains "CorrelationId propagado nos eventos"         "$RESULT_MESSAGE" "$CORRELATION_ID"
assert_contains "DiagramAnalysisProcessId propagado"         "$RESULT_MESSAGE" "$REQUEST_ID"

# ---------------------------------------------------------------------------
echo ""
echo "=== [4/5] Persistência no banco de dados ==="
JOB_STATUS=$(query_postgres "SELECT \"Status\" FROM \"DiagramProcessingJobs\" WHERE \"DiagramAnalysisProcessId\"='${REQUEST_ID}' LIMIT 1;")
assert_contains "Job persistido com status Completed" "$JOB_STATUS" "Completed"

JOB_COUNT=$(query_postgres "SELECT COUNT(*) FROM \"DiagramProcessingJobs\" WHERE \"DiagramAnalysisProcessId\"='${REQUEST_ID}';")
assert_contains "Exatamente 1 job criado" "$JOB_COUNT" "1"

RESULT_COUNT=$(query_postgres "SELECT COUNT(*) FROM \"DiagramProcessingResults\" r JOIN \"DiagramProcessingJobs\" j ON r.\"DiagramProcessingJobId\"=j.\"Id\" WHERE j.\"DiagramAnalysisProcessId\"='${REQUEST_ID}';")
assert_contains "Resultado de processamento persistido" "$RESULT_COUNT" "1"

ATTEMPT_STATUS=$(query_postgres "SELECT a.\"Status\" FROM \"DiagramProcessingAttempts\" a JOIN \"DiagramProcessingJobs\" j ON a.\"DiagramProcessingJobId\"=j.\"Id\" WHERE j.\"DiagramAnalysisProcessId\"='${REQUEST_ID}' ORDER BY a.\"AttemptNumber\" LIMIT 1;")
assert_contains "Attempt persistido com status Completed" "$ATTEMPT_STATUS" "Completed"

# ---------------------------------------------------------------------------
echo ""
echo "=== [5/5] Idempotência: mensagem duplicada não cria segundo job ==="
echo "[INFO] Reenviando a mesma mensagem..."
send_analysis_request "$REQUEST_ID"
sleep 5

JOB_COUNT_AFTER=$(query_postgres "SELECT COUNT(*) FROM \"DiagramProcessingJobs\" WHERE \"DiagramAnalysisProcessId\"='${REQUEST_ID}';")
assert_contains "Mensagem duplicada não cria segundo job" "$JOB_COUNT_AFTER" "1"

# ---------------------------------------------------------------------------
echo ""
echo "==================================================================="
echo "Resultado: $PASS passou(aram) | $FAIL falhou(aram)"
echo "==================================================================="

if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi

echo "[SUCCESS] Fluxo local completo validado."