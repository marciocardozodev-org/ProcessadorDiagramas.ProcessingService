# Guia de Desenvolvimento Local

## Objetivo da etapa atual

Validar a base do microservico com dominio, aplicacao e persistencia em EF Core antes de introduzir fila, storage e pipeline de IA.

## Loop local

```bash
dotnet build ProcessadorDiagramas.ProcessingService.sln
dotnet test ProcessadorDiagramas.ProcessingService.sln
```

## Subir banco e aplicar migrations

```bash
docker compose up postgres -d
dotnet ef database update \
	--project src/ProcessadorDiagramas.ProcessingService.Infrastructure/ProcessadorDiagramas.ProcessingService.Infrastructure.csproj \
	--startup-project src/ProcessadorDiagramas.ProcessingService.API/ProcessadorDiagramas.ProcessingService.API.csproj
```

## Smoke test com Docker Compose

```bash
docker compose up --build
curl http://localhost:5080/health
./scripts/test-docker-compose-flow.sh
```

O smoke test completo valida:

- PostgreSQL local
- migrations via efbundle
- fila SQS no LocalStack
- consumo de AnalysisProcessRequestedEvent
- publicação de AnalysisProcessingCompletedEvent

## Deploy rápido no Minikube

```bash
./scripts/minikube/build-local-image.sh
IMAGE_TAG=local ./scripts/minikube/deploy.sh
```

## Direcao das proximas etapas

- manter banco proprio para este servico
- consumir fila e registrar status tecnico de execucao
- persistir resultado bruto da IA como responsabilidade central do servico
- usar implementacoes locais e dummy enquanto o provider real de IA nao estiver conectado
- publicar eventos de inicio, sucesso e falha para integracao com o servico anterior
- manter API minima, sem endpoints publicos de cliente final
- evoluir a observabilidade operacional e a integracao com o provider real de IA