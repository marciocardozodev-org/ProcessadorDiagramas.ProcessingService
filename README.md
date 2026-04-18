# ProcessadorDiagramas.ProcessingService

Microservico de processamento de diagramas com perfil de worker de dominio e API minima para health e observabilidade.

## Etapas implementadas

- solution propria em .NET 8
- separacao em API, Application, Domain e Infrastructure
- modelagem de dominio para job, resultado bruto e tentativas de processamento
- contratos de aplicacao para criacao e consulta tecnica de jobs
- persistencia com EF Core, DbContext, repositorios e migration inicial
- abstracao de mensageria com consumidor de fila e handler do evento de entrada
- pipeline interno com leitura do arquivo, pre-processamento especializado por tipo e provider de IA configuravel com fallback dummy para persistencia do resultado bruto
- publicacao de eventos de inicio, sucesso e falha do processamento via barramento assincrono
- projeto de testes com smoke tests da API minima, dominio, aplicacao e persistencia
- host inicial com health checks e logs em JSON
- base para Docker Compose com API, PostgreSQL, LocalStack e migration job
- smoke test local do fluxo assíncrono via fila validado ponta a ponta
- manifests base de Kubernetes para deployment, service e migration job
- scripts auxiliares para build local no Minikube e deploy em namespace dedicado
- workflow de CI/CD dedicado para o ProcessingService

## Estrutura da solution

- src/ProcessadorDiagramas.ProcessingService.API
- src/ProcessadorDiagramas.ProcessingService.Application
- src/ProcessadorDiagramas.ProcessingService.Domain
- src/ProcessadorDiagramas.ProcessingService.Infrastructure
- tests/ProcessadorDiagramas.ProcessingService.Tests

## Responsabilidade do servico

Este servico sera responsavel por consumir eventos de solicitacao de processamento, carregar o diagrama no storage, executar pre-processamento, acionar a IA, persistir a saida bruta e publicar eventos de sucesso ou falha.

Nao faz parte deste servico:

- upload publico do cliente final
- orquestracao central de todo o fluxo
- geracao do relatorio final

## Rodar localmente

```bash
dotnet restore ProcessadorDiagramas.ProcessingService.sln
dotnet build ProcessadorDiagramas.ProcessingService.sln
dotnet test ProcessadorDiagramas.ProcessingService.sln
dotnet run --project src/ProcessadorDiagramas.ProcessingService.API
```

## Banco e migrations

Migration inicial gerada em:

- src/ProcessadorDiagramas.ProcessingService.Infrastructure/Data/Migrations

Aplicar localmente:

```bash
docker compose up postgres -d
dotnet ef database update \
	--project src/ProcessadorDiagramas.ProcessingService.Infrastructure/ProcessadorDiagramas.ProcessingService.Infrastructure.csproj \
	--startup-project src/ProcessadorDiagramas.ProcessingService.API/ProcessadorDiagramas.ProcessingService.API.csproj
```

Endpoints disponiveis nesta etapa:

- GET /health
- GET /ready
- GET /

## Docker Compose

```bash
docker compose up --build
```

API local:

- http://localhost:5080/health

Fluxo local completo com fila:

```bash
./scripts/test-docker-compose-flow.sh
```

Para usar provider real compatível com OpenAI, configure as variáveis do bloco AiProvider no ambiente da API, por exemplo:

```bash
export AiProvider__Enabled=true
export AiProvider__Provider=OpenAICompatible
export AiProvider__BaseUrl=https://api.openai.com/
export AiProvider__ApiKey=seu-token
export AiProvider__Model=gpt-4o-mini
```

O script sobe Postgres, LocalStack, aplica migrations, publica uma mensagem `AnalysisProcessRequestedEvent` na fila de entrada e valida o evento `AnalysisProcessingCompletedEvent` na fila de saída.

## Kubernetes

Arquivos base de deploy:

- deploy/k8s/deployment.yaml
- deploy/k8s/service.yaml
- deploy/k8s/create-db-job.yaml

Scripts auxiliares para cluster local:

```bash
./scripts/minikube/build-local-image.sh
IMAGE_TAG=local ./scripts/minikube/deploy.sh
```

## CI/CD

Workflow dedicado:

- .github/workflows/processing-service-ci-cd.yml

O pipeline restaura, compila, testa, gera a imagem Docker e executa o deploy em Kubernetes para homolog e master.

## Proximas etapas

1. Integrar provider real de IA e pre-processamento especializado por tipo de diagrama.
2. Evoluir a observabilidade operacional do worker com métricas e tracing.
3. Refinar contratos de evento com o serviço anterior conforme a integração final.