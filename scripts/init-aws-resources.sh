#!/usr/bin/env bash

set -euo pipefail

REGION="${AWS_REGION:-us-east-1}"
TOPIC_NAME="${TOPIC_NAME:-processador-diagramas-processingservice-topic}"
QUEUE_NAME="${QUEUE_NAME:-processador-diagramas-processingservice-queue}"
AWS_ENDPOINT_URL="${AWS_ENDPOINT_URL:-}"

aws_cmd() {
  if [[ -n "$AWS_ENDPOINT_URL" ]]; then
    aws --region "$REGION" --endpoint-url "$AWS_ENDPOINT_URL" "$@"
  else
    aws --region "$REGION" "$@"
  fi
}

echo "[init] region=$REGION topic=$TOPIC_NAME queue=$QUEUE_NAME"
if [[ -n "$AWS_ENDPOINT_URL" ]]; then
  echo "[init] endpoint=$AWS_ENDPOINT_URL"
fi

TOPIC_ARN="$(aws_cmd sns create-topic --name "$TOPIC_NAME" --query TopicArn --output text)"
QUEUE_URL="$(aws_cmd sqs create-queue --queue-name "$QUEUE_NAME" --query QueueUrl --output text)"
QUEUE_ARN="$(aws_cmd sqs get-queue-attributes --queue-url "$QUEUE_URL" --attribute-names QueueArn --query 'Attributes.QueueArn' --output text)"

EXISTING_SUB_ARN="$(aws_cmd sns list-subscriptions-by-topic --topic-arn "$TOPIC_ARN" --query "Subscriptions[?Endpoint=='$QUEUE_ARN'].SubscriptionArn | [0]" --output text || true)"

if [[ -z "$EXISTING_SUB_ARN" || "$EXISTING_SUB_ARN" == "None" ]]; then
  SUB_ARN="$(aws_cmd sns subscribe --topic-arn "$TOPIC_ARN" --protocol sqs --notification-endpoint "$QUEUE_ARN" --query SubscriptionArn --output text)"
  echo "[init] created subscription: $SUB_ARN"
else
  echo "[init] subscription already exists: $EXISTING_SUB_ARN"
fi

read -r -d '' QUEUE_POLICY <<JSON || true
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AllowSNSToSendMessage",
      "Effect": "Allow",
      "Principal": { "Service": "sns.amazonaws.com" },
      "Action": "sqs:SendMessage",
      "Resource": "$QUEUE_ARN",
      "Condition": {
        "ArnEquals": {
          "aws:SourceArn": "$TOPIC_ARN"
        }
      }
    }
  ]
}
JSON

QUEUE_POLICY_COMPACT="$(printf '%s' "$QUEUE_POLICY" | tr -d '\n' | sed 's/[[:space:]]\+/ /g')"

aws_cmd sqs set-queue-attributes \
  --queue-url "$QUEUE_URL" \
  --attributes "Policy=$QUEUE_POLICY_COMPACT" >/dev/null

echo "[init] done"
echo "TOPIC_ARN=$TOPIC_ARN"
echo "QUEUE_URL=$QUEUE_URL"
echo "QUEUE_ARN=$QUEUE_ARN"
