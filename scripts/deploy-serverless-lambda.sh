#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
AWS_REGION="${AWS_REGION:-us-east-1}"
AWS_LAMBDA_FUNCTION_NAME="${AWS_LAMBDA_FUNCTION_NAME:-}"
AWS_LAMBDA_ROLE_ARN="${AWS_LAMBDA_ROLE_ARN:-}"
AWS_ECR_REPOSITORY_NAME="${AWS_ECR_REPOSITORY_NAME:-processador-diagramas-processingservice-lambda}"
AWS_SQS_QUEUE_NAME="${AWS_SQS_QUEUE_NAME:-processador-diagramas-processingservice-input}"
AWS_SNS_TOPIC_NAME="${AWS_SNS_TOPIC_NAME:-processador-diagramas-processingservice-events}"
DIAGRAM_SOURCE_BUCKET_NAME="${DIAGRAM_SOURCE_BUCKET_NAME:-}"
DIAGRAM_SOURCE_KEY_PREFIX="${DIAGRAM_SOURCE_KEY_PREFIX:-inputs/}"
AWS_DB_CONNECTION_PARAMETER_ARN="${AWS_DB_CONNECTION_PARAMETER_ARN:-}"
AWS_AI_API_KEY_SECRET_ARN="${AWS_AI_API_KEY_SECRET_ARN:-}"
AI_PROVIDER_BASE_URL="${AI_PROVIDER_BASE_URL:-}"
AI_PROVIDER_MODEL="${AI_PROVIDER_MODEL:-}"
AWS_LAMBDA_MEMORY_SIZE="${AWS_LAMBDA_MEMORY_SIZE:-1024}"
AWS_LAMBDA_TIMEOUT="${AWS_LAMBDA_TIMEOUT:-120}"
AWS_LAMBDA_ARCHITECTURE="${AWS_LAMBDA_ARCHITECTURE:-x86_64}"
IMAGE_TAG="${IMAGE_TAG:-$(git -C "$ROOT_DIR" rev-parse --short HEAD 2>/dev/null || date +%s)}"

require_cmd() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "[ERROR] Comando obrigatório não encontrado: $cmd"
    exit 1
  fi
}

require_value() {
  local name="$1"
  local value="$2"
  if [[ -z "$value" ]]; then
    echo "[ERROR] $name é obrigatório"
    exit 1
  fi
}

json_escape() {
  jq -Rn --arg value "$1" '$value'
}

require_cmd aws
require_cmd docker
require_cmd jq

require_value AWS_LAMBDA_FUNCTION_NAME "$AWS_LAMBDA_FUNCTION_NAME"
require_value AWS_LAMBDA_ROLE_ARN "$AWS_LAMBDA_ROLE_ARN"
require_value DIAGRAM_SOURCE_BUCKET_NAME "$DIAGRAM_SOURCE_BUCKET_NAME"
require_value AWS_DB_CONNECTION_PARAMETER_ARN "$AWS_DB_CONNECTION_PARAMETER_ARN"
require_value AWS_AI_API_KEY_SECRET_ARN "$AWS_AI_API_KEY_SECRET_ARN"
require_value AI_PROVIDER_BASE_URL "$AI_PROVIDER_BASE_URL"
require_value AI_PROVIDER_MODEL "$AI_PROVIDER_MODEL"

ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text --region "$AWS_REGION")"
ECR_REGISTRY="${ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
ECR_URI="${ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/${AWS_ECR_REPOSITORY_NAME}"

echo "[INFO] Garantindo repositório ECR ${AWS_ECR_REPOSITORY_NAME}"
if ! aws ecr describe-repositories --repository-names "$AWS_ECR_REPOSITORY_NAME" --region "$AWS_REGION" >/dev/null 2>&1; then
  aws ecr create-repository --repository-name "$AWS_ECR_REPOSITORY_NAME" --region "$AWS_REGION" >/dev/null
fi

echo "[INFO] Efetuando login no ECR"
aws ecr get-login-password --region "$AWS_REGION" | docker login --username AWS --password-stdin "$ECR_REGISTRY" >/dev/null

echo "[INFO] Build da imagem Lambda"
docker build -f "$ROOT_DIR/Dockerfile.lambda" -t "$ECR_URI:$IMAGE_TAG" "$ROOT_DIR"

echo "[INFO] Push da imagem Lambda"
docker push "$ECR_URI:$IMAGE_TAG" >/dev/null

echo "[INFO] Garantindo tópico SNS de saída"
SNS_TOPIC_ARN="$(aws sns create-topic --name "$AWS_SNS_TOPIC_NAME" --region "$AWS_REGION" --query TopicArn --output text)"

echo "[INFO] Garantindo fila SQS de entrada"
QUEUE_URL=""
if ! QUEUE_URL="$(aws sqs get-queue-url --queue-name "$AWS_SQS_QUEUE_NAME" --region "$AWS_REGION" --query QueueUrl --output text 2>/dev/null)"; then
  QUEUE_URL="$(aws sqs create-queue --queue-name "$AWS_SQS_QUEUE_NAME" --region "$AWS_REGION" --query QueueUrl --output text)"
fi

QUEUE_ARN="$(aws sqs get-queue-attributes --queue-url "$QUEUE_URL" --attribute-names QueueArn --region "$AWS_REGION" --query 'Attributes.QueueArn' --output text)"

ENVIRONMENT_JSON="$(jq -nc \
  --arg defaultConnectionArn "$AWS_DB_CONNECTION_PARAMETER_ARN" \
  --arg aiSecretArn "$AWS_AI_API_KEY_SECRET_ARN" \
  --arg region "$AWS_REGION" \
  --arg topicArn "$SNS_TOPIC_ARN" \
  --arg queueUrl "$QUEUE_URL" \
  --arg bucketName "$DIAGRAM_SOURCE_BUCKET_NAME" \
  --arg keyPrefix "$DIAGRAM_SOURCE_KEY_PREFIX" \
  --arg aiBaseUrl "$AI_PROVIDER_BASE_URL" \
  --arg aiModel "$AI_PROVIDER_MODEL" \
  '{Variables:{EnableAwsServices:"true",Aws__Region:$region,Aws__TopicArn:$topicArn,Aws__QueueUrl:$queueUrl,Aws__EnableSqsPolling:"false",Aws__DbConnectionParameterArn:$defaultConnectionArn,Aws__AiApiKeySecretArn:$aiSecretArn,DiagramSourceStorage__Provider:"S3",DiagramSourceStorage__BucketName:$bucketName,DiagramSourceStorage__KeyPrefix:$keyPrefix,AiProvider__Enabled:"true",AiProvider__Provider:"OpenAICompatible",AiProvider__BaseUrl:$aiBaseUrl,AiProvider__Model:$aiModel,AiProvider__Temperature:"0.2",AiProvider__MaxTokens:"500",AiProvider__MaxInputCharacters:"12000",AiProvider__TimeoutSeconds:"60"}}')"

echo "[INFO] Publicando função Lambda ${AWS_LAMBDA_FUNCTION_NAME}"
if aws lambda get-function --function-name "$AWS_LAMBDA_FUNCTION_NAME" --region "$AWS_REGION" >/dev/null 2>&1; then
  aws lambda update-function-code \
    --function-name "$AWS_LAMBDA_FUNCTION_NAME" \
    --image-uri "$ECR_URI:$IMAGE_TAG" \
    --region "$AWS_REGION" >/dev/null

  aws lambda update-function-configuration \
    --function-name "$AWS_LAMBDA_FUNCTION_NAME" \
    --role "$AWS_LAMBDA_ROLE_ARN" \
    --timeout "$AWS_LAMBDA_TIMEOUT" \
    --memory-size "$AWS_LAMBDA_MEMORY_SIZE" \
    --architectures "$AWS_LAMBDA_ARCHITECTURE" \
    --environment "$ENVIRONMENT_JSON" \
    --region "$AWS_REGION" >/dev/null
else
  aws lambda create-function \
    --function-name "$AWS_LAMBDA_FUNCTION_NAME" \
    --package-type Image \
    --code ImageUri="$ECR_URI:$IMAGE_TAG" \
    --role "$AWS_LAMBDA_ROLE_ARN" \
    --timeout "$AWS_LAMBDA_TIMEOUT" \
    --memory-size "$AWS_LAMBDA_MEMORY_SIZE" \
    --architectures "$AWS_LAMBDA_ARCHITECTURE" \
    --environment "$ENVIRONMENT_JSON" \
    --region "$AWS_REGION" >/dev/null
fi

echo "[INFO] Garantindo event source mapping da fila SQS"
EVENT_SOURCE_MAPPING_UUID="$(aws lambda list-event-source-mappings --function-name "$AWS_LAMBDA_FUNCTION_NAME" --event-source-arn "$QUEUE_ARN" --region "$AWS_REGION" --query 'EventSourceMappings[0].UUID' --output text 2>/dev/null || true)"
if [[ -z "$EVENT_SOURCE_MAPPING_UUID" || "$EVENT_SOURCE_MAPPING_UUID" == "None" ]]; then
  aws lambda create-event-source-mapping \
    --function-name "$AWS_LAMBDA_FUNCTION_NAME" \
    --event-source-arn "$QUEUE_ARN" \
    --batch-size 10 \
    --enabled \
    --region "$AWS_REGION" >/dev/null
fi

aws lambda wait function-updated --function-name "$AWS_LAMBDA_FUNCTION_NAME" --region "$AWS_REGION"

echo "[SUCCESS] Lambda publicada"
echo "  Function: $AWS_LAMBDA_FUNCTION_NAME"
echo "  ECR: $ECR_URI:$IMAGE_TAG"
echo "  SNS Topic: $SNS_TOPIC_ARN"
echo "  SQS Queue: $QUEUE_URL"