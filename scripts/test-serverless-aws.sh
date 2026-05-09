#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
AWS_REGION="${AWS_REGION:-us-east-1}"
AWS_LAMBDA_FUNCTION_NAME="${AWS_LAMBDA_FUNCTION_NAME:-}"
DIAGRAM_SOURCE_BUCKET_NAME="${DIAGRAM_SOURCE_BUCKET_NAME:-}"
DIAGRAM_SOURCE_KEY_PREFIX="${DIAGRAM_SOURCE_KEY_PREFIX:-inputs/}"
TEST_DIAGRAM_KEY="${TEST_DIAGRAM_KEY:-e2e-serverless-$(date +%s).mmd}"
CORRELATION_ID="${CORRELATION_ID:-serverless-$(date +%s)}"

PASS=0
FAIL=0

require_cmd() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "[ERROR] Comando obrigatório não encontrado: $cmd"
    exit 1
  fi
}

fail() {
  echo "[FAIL] $1"
  FAIL=$((FAIL + 1))
}

pass() {
  echo "[PASS] $1"
  PASS=$((PASS + 1))
}

assert_contains() {
  local description="$1"
  local haystack="$2"
  local needle="$3"

  if echo "$haystack" | grep -q "$needle"; then
    pass "$description"
  else
    fail "$description"
    echo "       Esperado conter: $needle"
  fi
}

if [[ -z "$AWS_LAMBDA_FUNCTION_NAME" ]]; then
  echo "[ERROR] AWS_LAMBDA_FUNCTION_NAME é obrigatório"
  exit 1
fi

if [[ -z "$DIAGRAM_SOURCE_BUCKET_NAME" ]]; then
  echo "[ERROR] DIAGRAM_SOURCE_BUCKET_NAME é obrigatório"
  exit 1
fi

require_cmd aws
require_cmd jq
require_cmd base64
require_cmd grep

DIAGRAM_SOURCE_KEY_PREFIX="${DIAGRAM_SOURCE_KEY_PREFIX%/}/"
OBJECT_KEY="${DIAGRAM_SOURCE_KEY_PREFIX}${TEST_DIAGRAM_KEY}"
INPUT_FILE="$(mktemp)"
PAYLOAD_FILE="$(mktemp)"
RESPONSE_FILE="$(mktemp)"
LOG_FILE="$(mktemp)"

cleanup() {
  rm -f "$INPUT_FILE" "$PAYLOAD_FILE" "$RESPONSE_FILE" "$LOG_FILE"
}

trap cleanup EXIT

cat > "$INPUT_FILE" <<'EOF'
graph TD
  Start --> Parse
  Parse --> Process
  Process --> Complete
EOF

echo "[INFO] Uploading diagram to s3://${DIAGRAM_SOURCE_BUCKET_NAME}/${OBJECT_KEY}"
aws s3 cp "$INPUT_FILE" "s3://${DIAGRAM_SOURCE_BUCKET_NAME}/${OBJECT_KEY}" --region "$AWS_REGION" >/dev/null

DIAGRAM_ANALYSIS_PROCESS_ID="$(cat /proc/sys/kernel/random/uuid)"
REQUESTED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

INNER_PAYLOAD="$(jq -nc \
  --arg id "$DIAGRAM_ANALYSIS_PROCESS_ID" \
  --arg key "$TEST_DIAGRAM_KEY" \
  --arg correlationId "$CORRELATION_ID" \
  --arg requestedAt "$REQUESTED_AT" \
  '{DiagramAnalysisProcessId:$id,InputStorageKey:$key,CorrelationId:$correlationId,RequestedAt:$requestedAt}')"

SNS_BODY="$(jq -nc \
  --arg message "$INNER_PAYLOAD" \
  --arg eventType "AnalysisProcessRequestedEvent" \
  '{Type:"Notification",MessageId:"serverless-e2e",Message:$message,MessageAttributes:{eventType:{Type:"String",Value:$eventType}}}')"

jq -nc \
  --arg messageId "serverless-e2e-$(date +%s%N)" \
  --arg body "$SNS_BODY" \
  '{Records:[{messageId:$messageId,receiptHandle:"serverless-receipt",body:$body,attributes:{},messageAttributes:{},md5OfBody:"serverless",eventSource:"aws:sqs",eventSourceARN:"arn:aws:sqs:us-east-1:000000000000:serverless-input",awsRegion:"us-east-1"}]}' \
  > "$PAYLOAD_FILE"

echo "[INFO] Invoking Lambda ${AWS_LAMBDA_FUNCTION_NAME}"
LOG_RESULT_B64="$(aws lambda invoke \
  --function-name "$AWS_LAMBDA_FUNCTION_NAME" \
  --payload "file://$PAYLOAD_FILE" \
  --cli-binary-format raw-in-base64-out \
  --log-type Tail \
  --region "$AWS_REGION" \
  "$RESPONSE_FILE" \
  --query 'LogResult' \
  --output text)"

LOG_RESULT="$(printf '%s' "$LOG_RESULT_B64" | base64 --decode 2>/dev/null || true)"
printf '%s\n' "$LOG_RESULT" > "$LOG_FILE"

echo "[INFO] Lambda log tail"
cat "$LOG_FILE"

if [[ ! -s "$RESPONSE_FILE" ]]; then
  fail "Lambda não retornou payload de resposta"
else
  FAILURES_COUNT="$(jq '.BatchItemFailures // .batchItemFailures // [] | length' "$RESPONSE_FILE")"
  if [[ "$FAILURES_COUNT" == "0" ]]; then
    pass "Lambda retornou sem BatchItemFailures"
  else
    fail "Lambda retornou $FAILURES_COUNT BatchItemFailures"
    jq '.BatchItemFailures // .batchItemFailures' "$RESPONSE_FILE"
  fi
fi

assert_contains "Log indica início do processamento" "$LOG_RESULT" "AnalysisProcessingStartedEvent"
assert_contains "Log indica conclusão do processamento" "$LOG_RESULT" "AnalysisProcessingCompletedEvent"
assert_contains "CorrelationId propagado" "$LOG_RESULT" "$CORRELATION_ID"
assert_contains "DiagramAnalysisProcessId propagado" "$LOG_RESULT" "$DIAGRAM_ANALYSIS_PROCESS_ID"

echo ""
echo "================================"
echo " Resultado: PASS=$PASS  FAIL=$FAIL"
echo "================================"

if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi

echo "[SUCCESS] Smoke serverless validado."