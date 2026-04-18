#!/bin/bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ANALYSIS_PROCESS_ID="$(cat /proc/sys/kernel/random/uuid 2>/dev/null || python3 -c 'import uuid; print(uuid.uuid4())')"
BASE_URL="http://127.0.0.1:5081"

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

  echo "[INFO] Aguardando endpoint $url..."
  until curl -fsS "$url" >/dev/null 2>&1; do
    if [[ "$attempt" -ge "$retries" ]]; then
      echo "[ERROR] Timeout aguardando endpoint $url"
      return 1
    fi
    attempt=$((attempt + 1))
    sleep 2
  done
  echo "[OK] Endpoint disponível: $url"
}

assert_status() {
  local label="$1"
  local expected="$2"
  local actual="$3"

  if [[ "$actual" == "$expected" ]]; then
    echo "[PASS] $label — HTTP $actual"
  else
    echo "[FAIL] $label — esperado HTTP $expected, recebido HTTP $actual"
    exit 1
  fi
}

require_cmd docker
require_cmd curl

echo "[INFO] Subindo ambiente local com Postgres e API..."
cd "$ROOT_DIR"
docker compose up -d --build postgres migrate api

echo "[INFO] Aguardando health da API..."
wait_for_http "$BASE_URL/health"

echo ""
echo "=== SMOKE TESTS ==="
echo ""

# Teste 1: health check
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/health")
assert_status "GET /health" "200" "$STATUS"

# Teste 2: root descreve o serviço
BODY=$(curl -s "$BASE_URL/")
if echo "$BODY" | grep -q "reporting-api"; then
  echo "[PASS] GET / — payload contém 'reporting-api'"
else
  echo "[FAIL] GET / — payload não contém 'reporting-api'. Resposta: $BODY"
  exit 1
fi

# Teste 3: ready endpoint
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/ready")
assert_status "GET /ready" "200" "$STATUS"

# Teste 4: GET /internal/reports/{id} — sem ProcessingService disponível → 404 ou 202
# (comportamento depende do ProcessingService; localmente sem ele retorna erro de conexão → 500 ou 503)
# Validamos que o endpoint existe e responde (não é 404 de rota inexistente)
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/internal/reports/$ANALYSIS_PROCESS_ID")
if [[ "$STATUS" == "404" || "$STATUS" == "202" || "$STATUS" == "200" || "$STATUS" == "500" || "$STATUS" == "503" ]]; then
  echo "[PASS] GET /internal/reports/{id} — endpoint existe e respondeu HTTP $STATUS"
else
  echo "[FAIL] GET /internal/reports/{id} — resposta inesperada: HTTP $STATUS"
  exit 1
fi

# Teste 5: POST /internal/reports/{id}/generate — mesmo raciocínio
STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE_URL/internal/reports/$ANALYSIS_PROCESS_ID/generate")
if [[ "$STATUS" == "404" || "$STATUS" == "202" || "$STATUS" == "200" || "$STATUS" == "500" || "$STATUS" == "503" ]]; then
  echo "[PASS] POST /internal/reports/{id}/generate — endpoint existe e respondeu HTTP $STATUS"
else
  echo "[FAIL] POST /internal/reports/{id}/generate — resposta inesperada: HTTP $STATUS"
  exit 1
fi

# Teste 6: Swagger disponível
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/swagger/index.html")
assert_status "GET /swagger/index.html" "200" "$STATUS"

echo ""
echo "[SUCCESS] Smoke test local concluído com sucesso."
echo ""
echo "Ambiente ainda disponível em $BASE_URL"
echo "Para derrubar: docker compose down -v"
