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

if ! command -v kubectl >/dev/null 2>&1; then
  echo "[ERROR] kubectl não encontrado no PATH."
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
  -n "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

kubectl create secret generic "$APP_NAME-secrets" \
  --from-literal=ConnectionStrings__DefaultConnection="$DEFAULT_CONNECTION" \
  --from-literal=Aws__Region="$AWS_REGION" \
  --from-literal=Aws__TopicArn="$AWS_TOPIC_ARN" \
  --from-literal=Aws__QueueUrl="$AWS_QUEUE_URL" \
  -n "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

echo "[INFO] Executando migration job..."
kubectl -n "$NAMESPACE" delete job "$APP_NAME-migrations" --ignore-not-found=true
sed "s|\${IMAGE_TAG}|$IMAGE_TAG|g" "$ROOT_DIR/deploy/k8s/create-db-job.yaml" | kubectl -n "$NAMESPACE" apply -f -
kubectl wait --for=condition=complete --timeout=300s "job/$APP_NAME-migrations" -n "$NAMESPACE"

echo "[INFO] Aplicando deployment e service..."
kubectl -n "$NAMESPACE" apply -f "$ROOT_DIR/deploy/k8s/service.yaml"
sed "s|\${IMAGE_TAG}|$IMAGE_TAG|g" "$ROOT_DIR/deploy/k8s/deployment.yaml" | kubectl -n "$NAMESPACE" apply -f -
kubectl rollout status "deployment/$APP_NAME" -n "$NAMESPACE" --timeout=300s

echo "[SUCCESS] Deploy concluído no namespace $NAMESPACE com a imagem tag $IMAGE_TAG."
