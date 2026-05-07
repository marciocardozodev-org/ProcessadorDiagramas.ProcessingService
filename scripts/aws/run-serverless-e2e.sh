#!/usr/bin/env bash

set -euo pipefail

ARTIFACT_DIR="${ARTIFACT_DIR:-artifacts/e2e-serverless}"
AWS_REGION="${AWS_REGION:?AWS_REGION is required}"
INPUT_BUCKET="${INPUT_BUCKET:?INPUT_BUCKET is required}"
INPUT_PREFIX="${INPUT_PREFIX:-input-diagrams}"
INPUT_QUEUE_URL="${INPUT_QUEUE_URL:?INPUT_QUEUE_URL is required}"
OUTPUT_QUEUE_URL="${OUTPUT_QUEUE_URL:?OUTPUT_QUEUE_URL is required}"
WORKER_LOG_GROUP="${WORKER_LOG_GROUP:?WORKER_LOG_GROUP is required}"
PSQL_CONNECTION_STRING="${PSQL_CONNECTION_STRING:?PSQL_CONNECTION_STRING is required}"
E2E_TIMEOUT_SECONDS="${E2E_TIMEOUT_SECONDS:-300}"
E2E_MAX_LATENCY_SECONDS="${E2E_MAX_LATENCY_SECONDS:-180}"

mkdir -p "$ARTIFACT_DIR"

analysis_id="$(cat /proc/sys/kernel/random/uuid)"
correlation_id="e2e-${GITHUB_RUN_ID:-local}-$(date +%s)"
requested_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
key="$INPUT_PREFIX/$analysis_id/diagram.mmd"

cat > "$ARTIFACT_DIR/diagram.mmd" <<'EOF'
graph TD
  Gateway --> Queue
  Queue --> Processing
  Processing --> Reports
EOF

aws s3 cp "$ARTIFACT_DIR/diagram.mmd" "s3://$INPUT_BUCKET/$key" --region "$AWS_REGION"

payload="{\"DiagramAnalysisProcessId\":\"$analysis_id\",\"InputStorageKey\":\"$key\",\"CorrelationId\":\"$correlation_id\",\"RequestedAt\":\"$requested_at\"}"
printf '%s\n' "$payload" > "$ARTIFACT_DIR/input-event.json"

aws sqs send-message \
  --queue-url "$INPUT_QUEUE_URL" \
  --message-body "$payload" \
  --message-attributes '{"eventType":{"DataType":"String","StringValue":"AnalysisProcessRequestedEvent"}}' \
  --region "$AWS_REGION" \
  > "$ARTIFACT_DIR/sqs-send.json"

start_epoch="$(date +%s)"
received_file="$ARTIFACT_DIR/output-message.json"

while true; do
  now_epoch="$(date +%s)"
  elapsed="$((now_epoch - start_epoch))"

  if [[ "$elapsed" -gt "$E2E_TIMEOUT_SECONDS" ]]; then
    echo "Timeout waiting for output event" | tee "$ARTIFACT_DIR/error.txt"
    exit 1
  fi

  response="$(aws sqs receive-message \
    --queue-url "$OUTPUT_QUEUE_URL" \
    --max-number-of-messages 10 \
    --wait-time-seconds 10 \
    --region "$AWS_REGION")"

  messages_count="$(printf '%s' "$response" | jq '.Messages | length // 0')"
  if [[ "$messages_count" -eq 0 ]]; then
    continue
  fi

  found_match="false"
  while IFS= read -r message; do
    receipt_handle="$(printf '%s' "$message" | jq -r '.ReceiptHandle')"
    body="$(printf '%s' "$message" | jq -r '.Body')"
    sns_message="$(printf '%s' "$body" | jq -r '.Message // empty')"
    event_type="$(printf '%s' "$body" | jq -r '.MessageAttributes.eventType.Value // empty')"

    if [[ -z "$sns_message" ]]; then
      aws sqs delete-message --queue-url "$OUTPUT_QUEUE_URL" --receipt-handle "$receipt_handle" --region "$AWS_REGION" >/dev/null
      continue
    fi

    analysis_from_output="$(printf '%s' "$sns_message" | jq -r '.DiagramAnalysisProcessId // empty')"
    correlation_from_output="$(printf '%s' "$sns_message" | jq -r '.CorrelationId // empty')"

    if [[ "$analysis_from_output" == "$analysis_id" && "$correlation_from_output" == "$correlation_id" && "$event_type" == "AnalysisProcessingCompletedEvent" ]]; then
      printf '%s\n' "$sns_message" | jq '.' > "$received_file"
      found_match="true"
    fi

    aws sqs delete-message --queue-url "$OUTPUT_QUEUE_URL" --receipt-handle "$receipt_handle" --region "$AWS_REGION" >/dev/null
  done < <(printf '%s' "$response" | jq -c '.Messages[]')

  if [[ "$found_match" == "true" ]]; then
    break
  fi

done

job_id="$(jq -r '.DiagramProcessingJobId' "$received_file")"
result_id="$(jq -r '.ResultId // empty' "$received_file")"

if [[ -z "$job_id" || "$job_id" == "null" ]]; then
  echo "Invalid output event: missing job id" | tee "$ARTIFACT_DIR/error.txt"
  exit 1
fi

if [[ -z "$result_id" || "$result_id" == "null" ]]; then
  echo "Invalid output event: missing result id" | tee "$ARTIFACT_DIR/error.txt"
  exit 1
fi

query="SELECT j.\"Id\", j.\"Status\", r.\"Id\" AS \"ResultId\" FROM \"DiagramProcessingJobs\" j LEFT JOIN \"DiagramProcessingResults\" r ON r.\"DiagramProcessingJobId\" = j.\"Id\" WHERE j.\"DiagramAnalysisProcessId\" = '$analysis_id';"
psql "$PSQL_CONNECTION_STRING" -At -c "$query" > "$ARTIFACT_DIR/db-check.txt"

if ! grep -q "|Completed|" "$ARTIFACT_DIR/db-check.txt"; then
  echo "DB validation failed: status not Completed" | tee "$ARTIFACT_DIR/error.txt"
  exit 1
fi

if ! grep -q "$result_id" "$ARTIFACT_DIR/db-check.txt"; then
  echo "DB validation failed: result id not found" | tee "$ARTIFACT_DIR/error.txt"
  exit 1
fi

filter_pattern="{ $.correlation_id = \"$correlation_id\" && $.LogLevel = \"Error\" }"
aws logs filter-log-events \
  --log-group-name "$WORKER_LOG_GROUP" \
  --filter-pattern "$filter_pattern" \
  --start-time "$((start_epoch * 1000))" \
  --region "$AWS_REGION" \
  > "$ARTIFACT_DIR/error-logs.json"

error_count="$(jq '.events | length' "$ARTIFACT_DIR/error-logs.json")"
if [[ "$error_count" -gt 0 ]]; then
  echo "Found critical error logs for correlation id" | tee "$ARTIFACT_DIR/error.txt"
  exit 1
fi

end_epoch="$(date +%s)"
latency="$((end_epoch - start_epoch))"
if [[ "$latency" -gt "$E2E_MAX_LATENCY_SECONDS" ]]; then
  echo "Latency assertion failed: ${latency}s > ${E2E_MAX_LATENCY_SECONDS}s" | tee "$ARTIFACT_DIR/error.txt"
  exit 1
fi

jq -n \
  --arg analysisId "$analysis_id" \
  --arg correlationId "$correlation_id" \
  --arg jobId "$job_id" \
  --arg resultId "$result_id" \
  --arg latencySeconds "$latency" \
  '{analysisId:$analysisId,correlationId:$correlationId,jobId:$jobId,resultId:$resultId,latencySeconds:($latencySeconds|tonumber),status:"passed"}' \
  > "$ARTIFACT_DIR/summary.json"

echo "E2E serverless passed in ${latency}s"
