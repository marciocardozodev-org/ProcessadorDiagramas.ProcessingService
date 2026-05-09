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

### IA real em modo low-cost

Sugestão de baseline econômico para começar com custo controlado:

```bash
export AIPROVIDER__ENABLED=true
export AIPROVIDER__PROVIDER=OpenAICompatible
export AIPROVIDER__BASEURL=https://api.openai.com/
export AIPROVIDER__APIKEY=seu-token
export AIPROVIDER__MODEL=gpt-4o-mini
export AIPROVIDER__TEMPERATURE=0.2
export AIPROVIDER__MAXTOKENS=500
export AIPROVIDER__MAXINPUTCHARACTERS=12000
```

Com Docker Compose:

```bash
docker compose up -d --build
```

Com Minikube:

```bash
set -a && source scripts/minikube/ai-low-cost.env.example && set +a
./scripts/minikube/build-local-image.sh
IMAGE_TAG=local ./scripts/minikube/deploy.sh
```

Observação: o pipeline OpenAI-compatible agora aplica truncamento configurável da entrada com `AiProvider__MaxInputCharacters`, reduzindo tokens de prompt e custo por chamada. Se `AIPROVIDER__ENABLED=true`, o deploy valida a presença de `AIPROVIDER__APIKEY` antes de aplicar os manifests.

O script sobe Postgres, LocalStack, aplica migrations, publica uma mensagem `AnalysisProcessRequestedEvent` na fila de entrada e valida o evento `AnalysisProcessingCompletedEvent` na fila de saída.

### Smoke serverless em AWS

O projeto também possui um caminho serverless real baseado em AWS Lambda, sem depender de alterar policies de SNS/SQS no ambiente.

O smoke test direto em AWS fica em:

- .github/workflows/serverless-aws-e2e.yml

Ele faz upload do diagrama para S3, invoca a Lambda com um payload SQS sintético e valida o log de processamento.

Para executar, configure os secrets de AWS e informe os inputs do workflow manual:

- nome da Lambda
- bucket S3 de entrada
- prefixo do key do diagrama

### Bootstrap one-time de SNS/SQS

Para ambientes onde os recursos de mensageria ainda não existem, use o script one-time:

- scripts/init-aws-resources.sh

Exemplo (AWS real):

```bash
AWS_REGION=us-east-1 \
TOPIC_NAME=processador-diagramas-processingservice-hml-topic \
QUEUE_NAME=processador-diagramas-processingservice-hml-queue \
./scripts/init-aws-resources.sh
```

Exemplo (LocalStack):

```bash
AWS_REGION=us-east-1 \
AWS_ENDPOINT_URL=http://localhost:4566 \
TOPIC_NAME=processador-diagramas-processingservice-topic \
QUEUE_NAME=processador-diagramas-processingservice-queue \
./scripts/init-aws-resources.sh
```

O runtime do serviço não deve criar/alterar infraestrutura de SNS/SQS; em execução ele apenas publica, consome e remove mensagens.

### Deploy serverless em AWS Lambda

Para publicar a aplicação como Lambda container image, use o workflow manual:

- .github/workflows/serverless-lambda-deploy.yml

Esse fluxo cria/atualiza a imagem no ECR, publica a função Lambda, provisiona uma fila SQS de entrada e configura o event source mapping da fila para a Lambda. O tópico SNS continua sendo usado para os eventos de saída do processamento.

Você precisa informar:

- ARN da role de execução da Lambda
- ARN do parâmetro SSM com a connection string
- ARN do secret com a chave da IA
- bucket S3 e prefixo dos diagramas

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