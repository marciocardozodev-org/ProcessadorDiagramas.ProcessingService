#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
NAMESPACE="${K8S_NAMESPACE:-reporting-local}"
IMAGE_TAG="${IMAGE_TAG:-local}"
APP_NAME="processador-diagramas-reportingservice"
DEFAULT_CONNECTION="${CONNECTIONSTRINGS__DEFAULTCONNECTION:-Host=postgresql.default.svc.cluster.local;Port=5432;Database=processador_diagramas_reporting;Username=postgres;Password=postgres}"
PROCESSING_SERVICE_BASE_URL="${PROCESSING_SERVICE__BASEURL:-http://processador-diagramas-processingservice.processing-local.svc.cluster.local}"
PROCESSING_SERVICE_TIMEOUT="${PROCESSING_SERVICE__TIMEOUTSECONDS:-30}"

if ! command -v kubectl >/dev/null 2>&1; then
  echo "[ERROR] kubectl não encontrado no PATH."
  exit 1
fi

echo "[INFO] Garantindo namespace $NAMESPACE..."
kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

echo "[INFO] Aplicando configmap do serviço..."
kubectl create configmap "$APP_NAME-config" \
  --from-literal=ASPNETCORE_ENVIRONMENT="Development" \
  --from-literal=ASPNETCORE_URLS="http://+:8080" \
  --from-literal=Service__Name="ProcessadorDiagramas.ReportingService" \
  --from-literal=ProcessingService__BaseUrl="$PROCESSING_SERVICE_BASE_URL" \
  --from-literal=ProcessingService__TimeoutSeconds="$PROCESSING_SERVICE_TIMEOUT" \
  -n "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

echo "[INFO] Aplicando secret do serviço..."
kubectl create secret generic "$APP_NAME-secrets" \
  --from-literal=ConnectionStrings__DefaultConnection="$DEFAULT_CONNECTION" \
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
echo ""
echo "Para testar localmente via port-forward:"
echo "  kubectl port-forward -n $NAMESPACE svc/$APP_NAME 5081:80"
echo "  curl http://localhost:5081/health"
echo "  curl http://localhost:5081/swagger"
