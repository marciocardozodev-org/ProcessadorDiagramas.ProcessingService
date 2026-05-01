#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
NAMESPACE="${K8S_NAMESPACE:-processing-local}"
IMAGE_TAG="${IMAGE_TAG:-local}"
APP_NAME="processador-diagramas-processingservice"
DEFAULT_CONNECTION="${CONNECTIONSTRINGS__DEFAULTCONNECTION:-Host=postgresql.default.svc.cluster.local;Port=5432;Database=processador_diagramas_processing;Username=postgres;Password=postgres}"
AWS_REGION="${AWS_REGION:-us-east-1}"
AWS_TOPIC_ARN="${AWS__TOPICARN:-arn:aws:sns:us-east-1:000000000000:analysis-processing-events}"
AWS_QUEUE_URL="${AWS__QUEUEURL:-http://localstack.localstack.svc.cluster.local:4566/000000000000/analysis-process-requests}"
AWS_SERVICE_URL="${AWS__SERVICEURL:-}" 
AI_PROVIDER_ENABLED="${AIPROVIDER__ENABLED:-false}"
AI_PROVIDER_NAME="${AIPROVIDER__PROVIDER:-OpenAICompatible}"
AI_PROVIDER_BASE_URL="${AIPROVIDER__BASEURL:-https://api.openai.com/}"
AI_PROVIDER_MODEL="${AIPROVIDER__MODEL:-gpt-4o-mini}"
AI_PROVIDER_TEMPERATURE="${AIPROVIDER__TEMPERATURE:-0.2}"
AI_PROVIDER_MAX_TOKENS="${AIPROVIDER__MAXTOKENS:-500}"
AI_PROVIDER_MAX_INPUT_CHARACTERS="${AIPROVIDER__MAXINPUTCHARACTERS:-12000}"
AI_PROVIDER_TIMEOUT_SECONDS="${AIPROVIDER__TIMEOUTSECONDS:-60}"
AI_PROVIDER_API_KEY="${AIPROVIDER__APIKEY:-}"

if ! command -v kubectl >/dev/null 2>&1; then
  echo "[ERROR] kubectl não encontrado no PATH."
  exit 1
fi

if [[ "${AI_PROVIDER_ENABLED,,}" == "true" ]] && [[ -z "$AI_PROVIDER_API_KEY" ]]; then
  echo "[ERROR] AIPROVIDER__APIKEY não definido. Defina a chave para executar com IA real."
  exit 1
fi

echo "[INFO] Garantindo namespace $NAMESPACE..."
kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

echo "[INFO] Aplicando configmap e secret do serviço..."
kubectl create configmap "$APP_NAME-config" \
  --from-literal=ASPNETCORE_ENVIRONMENT="Development" \
  --from-literal=ASPNETCORE_URLS="http://+:8080" \
  --from-literal=EnableAwsServices="true" \
  --from-literal=Service__Name="ProcessadorDiagramas.ProcessingService" \
  --from-literal=AiProvider__Enabled="$AI_PROVIDER_ENABLED" \
  --from-literal=AiProvider__Provider="$AI_PROVIDER_NAME" \
  --from-literal=AiProvider__BaseUrl="$AI_PROVIDER_BASE_URL" \
  --from-literal=AiProvider__Model="$AI_PROVIDER_MODEL" \
  --from-literal=AiProvider__Temperature="$AI_PROVIDER_TEMPERATURE" \
  --from-literal=AiProvider__MaxTokens="$AI_PROVIDER_MAX_TOKENS" \
  --from-literal=AiProvider__MaxInputCharacters="$AI_PROVIDER_MAX_INPUT_CHARACTERS" \
  --from-literal=AiProvider__TimeoutSeconds="$AI_PROVIDER_TIMEOUT_SECONDS" \
  -n "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

kubectl create secret generic "$APP_NAME-secrets" \
  --from-literal=ConnectionStrings__DefaultConnection="$DEFAULT_CONNECTION" \
  --from-literal=Aws__Region="$AWS_REGION" \
  --from-literal=Aws__TopicArn="$AWS_TOPIC_ARN" \
  --from-literal=Aws__QueueUrl="$AWS_QUEUE_URL" \
  --from-literal=Aws__ServiceURL="$AWS_SERVICE_URL" \
  --from-literal=AiProvider__ApiKey="$AI_PROVIDER_API_KEY" \
  --from-literal=AWS_ACCESS_KEY_ID="test" \
  --from-literal=AWS_SECRET_ACCESS_KEY="test" \
  -n "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

echo "[INFO] Executando migration job..."
kubectl -n "$NAMESPACE" delete job "$APP_NAME-migrations" --ignore-not-found=true
sed "s|\${IMAGE_TAG}|$IMAGE_TAG|g" "$ROOT_DIR/deploy/k8s/create-db-job.yaml" \
  | sed 's/imagePullPolicy: Always/imagePullPolicy: Never/g' \
  | kubectl -n "$NAMESPACE" apply -f -
kubectl wait --for=condition=complete --timeout=300s "job/$APP_NAME-migrations" -n "$NAMESPACE"

echo "[INFO] Aplicando deployment e service..."
kubectl -n "$NAMESPACE" apply -f "$ROOT_DIR/deploy/k8s/service.yaml"
sed "s|\${IMAGE_TAG}|$IMAGE_TAG|g" "$ROOT_DIR/deploy/k8s/deployment.yaml" \
  | sed 's/imagePullPolicy: Always/imagePullPolicy: Never/g' \
  | kubectl -n "$NAMESPACE" apply -f -
kubectl rollout status "deployment/$APP_NAME" -n "$NAMESPACE" --timeout=300s

echo "[SUCCESS] Deploy concluído no namespace $NAMESPACE com a imagem tag $IMAGE_TAG."
