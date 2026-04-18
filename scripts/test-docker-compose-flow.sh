#!/bin/bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INPUT_DIR="$ROOT_DIR/tmp/inputs"
INPUT_FILE_HOST="$INPUT_DIR/diagram-test.mmd"
INPUT_FILE_CONTAINER="/tmp/processing-inputs/diagram-test.mmd"
REQUEST_ID="$(cat /proc/sys/kernel/random/uuid)"
CORRELATION_ID="corr-$(date +%s)"

require_cmd() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "[ERROR] Comando obrigatório não encontrado: $cmd"
    exit 1
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

wait_for_result_message() {
  local retries=60
  local attempt=1

  while true; do
    local output
    output=$(docker exec processador_diagramas_processing_service_localstack awslocal sqs receive-message \
      --queue-url http://localhost:4566/000000000000/analysis-processing-results \
      --message-attribute-names All \
      --max-number-of-messages 10 \
      --visibility-timeout 1 2>/dev/null || true)

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

require_cmd docker
require_cmd curl
require_cmd grep

mkdir -p "$INPUT_DIR"
cat > "$INPUT_FILE_HOST" <<'EOF'
graph TD
  Gateway --> Processing
  Processing --> Reports
EOF

echo "[INFO] Subindo ambiente local com Postgres, LocalStack e API..."
cd "$ROOT_DIR"
docker compose up -d --build postgres localstack migrate api

echo "[INFO] Aguardando health da API..."
wait_for_http "http://127.0.0.1:5080/health"

echo "[INFO] Publicando mensagem de entrada na fila..."
docker exec processador_diagramas_processing_service_localstack awslocal sqs send-message \
  --queue-url http://localhost:4566/000000000000/analysis-process-requests \
  --message-body "{\"DiagramAnalysisProcessId\":\"$REQUEST_ID\",\"InputStorageKey\":\"$INPUT_FILE_CONTAINER\",\"CorrelationId\":\"$CORRELATION_ID\",\"RequestedAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}" \
  --message-attributes '{"eventType":{"StringValue":"AnalysisProcessRequestedEvent","DataType":"String"}}' >/dev/null

echo "[INFO] Aguardando evento de saída..."
RESULT_MESSAGE="$(wait_for_result_message)"

if ! echo "$RESULT_MESSAGE" | grep -q 'AnalysisProcessingCompletedEvent'; then
  echo "[ERROR] Evento de conclusão não encontrado na fila de saída."
  echo "$RESULT_MESSAGE"
  exit 1
fi

echo "[SUCCESS] Fluxo local completo validado."
echo "Result message: $RESULT_MESSAGE"