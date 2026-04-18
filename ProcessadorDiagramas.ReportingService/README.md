# ProcessadorDiagramas.ReportingService

Microserviço responsável por **compor e persistir relatórios técnicos estruturados** a partir dos dados brutos de análise de diagramas produzidos pelo ProcessingService.

## Responsabilidade

- Montar relatório técnico com componentes identificados, riscos arquiteturais e recomendações
- Persistir relatórios em banco próprio (PostgreSQL)
- Expor endpoints internos HTTP REST para consumo pelo API Gateway / BFF
- Consultar o ProcessingService via REST para obter resultados brutos quando necessário
- Suportar reconsulta rápida de relatórios já gerados (estratégia de cache persistido)

## Endpoints internos

| Método | Rota | Descrição |
|--------|------|-----------|
| `GET`  | `/internal/reports/{analysisProcessId}` | Retorna o relatório (gera sob demanda se ainda não existir) |
| `POST` | `/internal/reports/{analysisProcessId}/generate` | Força regeneração do relatório (nova versão) |
| `GET`  | `/health` | Health check |
| `GET`  | `/ready` | Readiness check |
| `GET`  | `/` | Informações do serviço |

### Códigos de resposta

| Código | Significado |
|--------|-------------|
| `200`  | Relatório disponível e retornado |
| `202`  | Processamento ainda não concluído — tente novamente mais tarde |
| `404`  | Processo de análise não encontrado no ProcessingService |

## Estrutura da solução

```
ProcessadorDiagramas.ReportingService/
├── src/
│   ├── ProcessadorDiagramas.ReportingService.Domain/        # Entidades, enums, interfaces
│   ├── ProcessadorDiagramas.ReportingService.Application/   # Handlers, queries, commands, contratos
│   ├── ProcessadorDiagramas.ReportingService.Infrastructure/ # EF Core, repositórios, client REST
│   └── ProcessadorDiagramas.ReportingService.API/           # Program.cs, controllers, Swagger
├── tests/
│   └── ProcessadorDiagramas.ReportingService.Tests/
├── deploy/k8s/
├── Dockerfile
└── docker-compose.yml
```

## Banco de dados

PostgreSQL próprio — não compartilhado com outros serviços.

**Tabela principal:** `AnalysisReports`

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `Id` | UUID | PK |
| `AnalysisProcessId` | UUID | Referência ao processo de análise (único) |
| `Status` | string | `Pending`, `Generated`, `Failed` |
| `ComponentsSummary` | text | JSON com componentes identificados |
| `ArchitecturalRisks` | text | JSON com riscos arquiteturais |
| `Recommendations` | text | JSON com recomendações |
| `SourceAnalysisReference` | string | ID do job do ProcessingService que originou o relatório |
| `Version` | int | Versão do relatório (incrementa a cada regeneração) |
| `FailureReason` | string | Motivo de falha, se aplicável |
| `GeneratedAt` | datetime | Quando o relatório foi gerado |
| `CreatedAt` | datetime | Criação do registro |
| `UpdatedAt` | datetime | Última atualização |

## Variáveis de ambiente

| Variável | Descrição | Exemplo |
|----------|-----------|---------|
| `ConnectionStrings__DefaultConnection` | Connection string PostgreSQL | `Host=postgres;Port=5432;Database=processador_diagramas_reporting;Username=postgres;Password=postgres` |
| `ProcessingService__BaseUrl` | URL base do ProcessingService | `http://processador-diagramas-processingservice` |
| `ProcessingService__TimeoutSeconds` | Timeout HTTP para o ProcessingService | `30` |
| `ASPNETCORE_ENVIRONMENT` | Ambiente | `Development` / `Production` |

## Execução local

### Pré-requisitos

- .NET 8 SDK
- PostgreSQL local ou via Docker

### Subir com Docker Compose

```bash
cd ProcessadorDiagramas.ReportingService
docker-compose up --build
```

A API ficará disponível em `http://localhost:5081`.  
Swagger em `http://localhost:5081/swagger`.

### Rodar localmente (sem Docker)

```bash
# Inicie um PostgreSQL na porta 5435
docker run -e POSTGRES_DB=processador_diagramas_reporting \
           -e POSTGRES_USER=postgres \
           -e POSTGRES_PASSWORD=postgres \
           -p 5435:5432 postgres:15-alpine

# Aplicar migrations
dotnet ef database update \
  --project src/ProcessadorDiagramas.ReportingService.Infrastructure \
  --startup-project src/ProcessadorDiagramas.ReportingService.API

# Executar
dotnet run --project src/ProcessadorDiagramas.ReportingService.API
```

### Rodar testes

```bash
dotnet test
```

## Deploy no Minikube

```bash
# Build da imagem localmente para o minikube
eval $(minikube docker-env)
docker build -t marciocardozodev/processador-diagramas-reportingservice:local .

# Criar secret com a connection string
kubectl create secret generic processador-diagramas-reportingservice-secrets \
  --from-literal=ConnectionStrings__DefaultConnection="Host=<postgres-host>;Port=5432;Database=processador_diagramas_reporting;Username=postgres;Password=postgres"

# Aplicar manifests
kubectl apply -f deploy/k8s/configmap.yaml
kubectl apply -f deploy/k8s/service.yaml
kubectl apply -f deploy/k8s/create-db-job.yaml  # Migrations
kubectl apply -f deploy/k8s/deployment.yaml
```

## Dependências de outros serviços

| Serviço | Comunicação | Endpoint consultado |
|---------|-------------|---------------------|
| ProcessingService | HTTP REST (interno) | `GET /internal/jobs/by-analysis-process/{analysisProcessId}` |
| API Gateway / BFF | HTTP REST (entrada) | Consome os endpoints deste serviço |

## Estratégia de geração de relatórios

**Sob demanda com cache persistido:**

1. `GET /internal/reports/{id}` → verifica se já existe relatório `Generated` no banco → retorna (`200`)
2. Se não existe → consulta ProcessingService → compõe relatório → persiste → retorna (`200`)
3. Se processamento ainda não concluiu → persiste como `Pending` → retorna (`202`)
4. `POST /internal/reports/{id}/generate` → força nova versão do relatório (incrementa `Version`)
