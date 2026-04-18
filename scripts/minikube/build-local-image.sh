#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
MINIKUBE_PROFILE="${MINIKUBE_PROFILE:-minikube}"
IMAGE_TAG="${IMAGE_TAG:-local}"
IMAGE_NAME="marciocardozodev/processador-diagramas-processingservice:${IMAGE_TAG}"

if ! command -v minikube >/dev/null 2>&1; then
  echo "[ERROR] minikube não encontrado no PATH."
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "[ERROR] docker não encontrado no PATH."
  exit 1
fi

STATUS="$(minikube -p "$MINIKUBE_PROFILE" status --format '{{.Host}}' 2>/dev/null || true)"
if [[ "$STATUS" != "Running" ]]; then
  echo "[ERROR] O perfil $MINIKUBE_PROFILE não está em execução. Inicie o Minikube antes de continuar."
  exit 1
fi

eval "$(minikube -p "$MINIKUBE_PROFILE" docker-env)"

cd "$ROOT_DIR"
docker build -t "$IMAGE_NAME" -f Dockerfile .

echo "[SUCCESS] Imagem local gerada para o Minikube: $IMAGE_NAME"
