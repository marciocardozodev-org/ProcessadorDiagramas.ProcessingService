#!/usr/bin/env bash

set -euo pipefail

REGION="${AWS_REGION:-us-east-1}"
TOPIC_NAME="${TOPIC_NAME:-processador-diagramas-processingservice-topic}"
QUEUE_NAME="${QUEUE_NAME:-processador-diagramas-processingservice-queue}"
LAMBDA_FUNCTION_NAME="${LAMBDA_FUNCTION_NAME:-}"
ECR_REPOSITORY_NAME="${ECR_REPOSITORY_NAME:-processador-diagramas-processingservice-lambda}"
AWS_ENDPOINT_URL="${AWS_ENDPOINT_URL:-}"

aws_cmd() {
  if [[ -n "$AWS_ENDPOINT_URL" ]]; then
    aws --region "$REGION" --endpoint-url "$AWS_ENDPOINT_URL" "$@"
  else
    aws --region "$REGION" "$@"
  fi
}

echo "[cleanup] region=$REGION"
echo "[cleanup] topic=$TOPIC_NAME"
echo "[cleanup] queue=$QUEUE_NAME"
if [[ -n "$LAMBDA_FUNCTION_NAME" ]]; then
  echo "[cleanup] lambda=$LAMBDA_FUNCTION_NAME"
fi
if [[ -n "$AWS_ENDPOINT_URL" ]]; then
  echo "[cleanup] endpoint=$AWS_ENDPOINT_URL (LocalStack)"
fi

read -p "⚠️  This will DELETE SNS topics, SQS queues, and optionally Lambda functions and ECR images. Continue? (yes/no): " -r
if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
  echo "[cleanup] Aborted."
  exit 0
fi

echo ""
echo "[cleanup] Removing SNS topic: $TOPIC_NAME"
TOPIC_ARN="$(aws_cmd sns list-topics --query "Topics[?contains(TopicArn, '$TOPIC_NAME')].TopicArn | [0]" --output text 2>/dev/null || true)"
if [[ -n "$TOPIC_ARN" && "$TOPIC_ARN" != "None" ]]; then
  aws_cmd sns delete-topic --topic-arn "$TOPIC_ARN" >/dev/null 2>&1 || true
  echo "  ✓ Topic deleted: $TOPIC_ARN"
else
  echo "  ⊘ Topic not found (may have been deleted already)"
fi

echo ""
echo "[cleanup] Removing SQS queue: $QUEUE_NAME"
QUEUE_URL="$(aws_cmd sqs get-queue-url --queue-name "$QUEUE_NAME" --query QueueUrl --output text 2>/dev/null || true)"
if [[ -n "$QUEUE_URL" && "$QUEUE_URL" != "None" ]]; then
  aws_cmd sqs delete-queue --queue-url "$QUEUE_URL" >/dev/null 2>&1 || true
  echo "  ✓ Queue deleted: $QUEUE_URL"
else
  echo "  ⊘ Queue not found (may have been deleted already)"
fi

if [[ -n "$LAMBDA_FUNCTION_NAME" ]]; then
  echo ""
  echo "[cleanup] Removing Lambda function: $LAMBDA_FUNCTION_NAME"
  if aws_cmd lambda get-function --function-name "$LAMBDA_FUNCTION_NAME" >/dev/null 2>&1; then
    aws_cmd lambda delete-function --function-name "$LAMBDA_FUNCTION_NAME" >/dev/null 2>&1 || true
    echo "  ✓ Function deleted: $LAMBDA_FUNCTION_NAME"
  else
    echo "  ⊘ Function not found (may have been deleted already)"
  fi
fi

echo ""
echo "[cleanup] Removing ECR repository images: $ECR_REPOSITORY_NAME"
if aws_cmd ecr describe-repositories --repository-names "$ECR_REPOSITORY_NAME" >/dev/null 2>&1; then
  IMAGE_IDS="$(aws_cmd ecr list-images --repository-name "$ECR_REPOSITORY_NAME" --query 'imageIds[*]' --output json)"
  if [[ "$IMAGE_IDS" != "[]" ]]; then
    aws_cmd ecr batch-delete-image \
      --repository-name "$ECR_REPOSITORY_NAME" \
      --image-ids "$IMAGE_IDS" >/dev/null 2>&1 || true
    echo "  ✓ ECR images deleted: $ECR_REPOSITORY_NAME"
  else
    echo "  ⊘ No images found in ECR (repository is empty)"
  fi
  
  echo ""
  read -p "  Delete ECR repository itself? (yes/no): " -r
  if [[ $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
    aws_cmd ecr delete-repository --repository-name "$ECR_REPOSITORY_NAME" --force >/dev/null 2>&1 || true
    echo "  ✓ ECR repository deleted: $ECR_REPOSITORY_NAME"
  fi
else
  echo "  ⊘ ECR repository not found"
fi

echo ""
echo "[cleanup] ✓ Cleanup complete"
echo ""
echo "Resources removed:"
echo "  • SNS topic: $TOPIC_NAME"
echo "  • SQS queue: $QUEUE_NAME"
if [[ -n "$LAMBDA_FUNCTION_NAME" ]]; then
  echo "  • Lambda function: $LAMBDA_FUNCTION_NAME"
fi
echo "  • ECR images: $ECR_REPOSITORY_NAME"
