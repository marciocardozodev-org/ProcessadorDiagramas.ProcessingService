# Alinhamento do ProcessingService ao Contrato Mestre de Integração

## Status de Implementação

✅ **Completo** - ProcessingService está totalmente alinhado ao contrato mestre de fluxo de integração.

---

## 1. Confirmar Responsabilidades do Worker de Domínio

✅ **CONFIRMADO** - ProcessingService implementa todas as responsabilidades:

- ✅ Consome `AnalysisProcessRequestedEvent` da fila (SQS)
- ✅ Lê arquivo do storage (S3 ou Local) via `InputStorageKey`
- ✅ Pré-processa o diagrama (especializado por tipo)
- ✅ Chama pipeline de IA (com fallback Dummy)
- ✅ Persiste resultado bruto em `DiagramProcessingResult`
- ✅ Publica eventos de ciclo de vida (Início, Sucesso, Falha)

**Handler Principal**: [ProcessDiagramProcessingJobCommandHandler](src/ProcessadorDiagramas.ProcessingService.Application/Commands/ProcessDiagramProcessingJob/ProcessDiagramProcessingJobCommandHandler.cs)

**Event Handler de Entrada**: [AnalysisProcessRequestedEventHandler](src/ProcessadorDiagramas.ProcessingService.Application/EventHandlers/AnalysisProcessRequestedEventHandler.cs)

---

## 2. Eventos Publicados Padronizados

✅ **IMPLEMENTADO COM VERSIONAMENTO** - Todos os três eventos canônicos:

### 2.1 AnalysisProcessingStartedEvent (v1 + compatível)
```csharp
{
  "diagramProcessingJobId": "{Guid}",
  "diagramAnalysisProcessId": "{Guid}",
  "correlationId": "{string}",
  "attemptNumber": {int},
  "startedAt": "{DateTime:UTC}"
}
```

### 2.2 AnalysisProcessingCompletedEvent (v1 + compatível)
```csharp
{
  "diagramProcessingJobId": "{Guid}",
  "diagramAnalysisProcessId": "{Guid}",
  "correlationId": "{string}",
  "resultId": "{Guid}",
  "attemptNumber": {int},
  "completedAt": "{DateTime:UTC}"
}
```

### 2.3 AnalysisProcessingFailedEvent (v1 + compatível)
```csharp
{
  "diagramProcessingJobId": "{Guid}",
  "diagramAnalysisProcessId": "{Guid}",
  "correlationId": "{string}",
  "attemptNumber": {int},
  "failureReason": "{string}",
  "failedAt": "{DateTime:UTC}"
}
```

**Arquivo de Contratos**: [Events/](src/ProcessadorDiagramas.ProcessingService.Application/Contracts/Events/)

---

## 3. Payload V2 - Contrato Canônico para ReportingService

✅ **IMPLEMENTADO COM FEATURE FLAGS** - V2 habilitados por padrão:

### 3.1 AnalysisProcessingCompletedV2Event (Contrato Canônico)

```json
{
  "eventVersion": "2.0.0",
  "eventType": "AnalysisProcessingCompletedV2Event",
  "occurredAtUtc": "{DateTime:UTC}",
  "correlationId": "{string}",
  "diagramAnalysisProcessId": "{Guid}",
  "diagramProcessingJobId": "{Guid}",
  "resultId": "{Guid}",
  "attemptNumber": {int},
  "processingStatus": "Completed",
  "rawAiOutput": "{string}",
  "outputHash": "sha256:{hex}",
  "trace": {
    "producerService": "ProcessadorDiagramas.ProcessingService",
    "producerVersion": "X.X.X",
    "messageId": "{UUID}"
  }
}
```

**Validações**:
- ✅ Campo `rawAiOutput` sempre populado
- ✅ `outputHash` é sempre SHA256 (começando com "sha256:")
- ✅ `trace.messageId` gerado por execução
- ✅ Serialização em **camelCase** (JSON PropertyName)

### 3.2 AnalysisProcessingFailedV2Event (Contrato Canônico)

```json
{
  "eventVersion": "2.0.0",
  "eventType": "AnalysisProcessingFailedV2Event",
  "occurredAtUtc": "{DateTime:UTC}",
  "correlationId": "{string}",
  "diagramAnalysisProcessId": "{Guid}",
  "diagramProcessingJobId": "{Guid}",
  "attemptNumber": {int},
  "failureReason": "{string}",
  "failureCode": "{string|null}",
  "trace": {
    "producerService": "ProcessadorDiagramas.ProcessingService",
    "producerVersion": "X.X.X",
    "messageId": "{UUID}"
  }
}
```

**Configuração**: [appsettings.json](src/ProcessadorDiagramas.ProcessingService.API/appsettings.json)

```json
"Messaging": {
  "PublishCompletedV2Enabled": true,    // Ativa V2 Completed
  "PublishFailedV2Enabled": true,       // Ativa V2 Failed
  "ProducerService": "ProcessadorDiagramas.ProcessingService",
  "ProducerVersion": ""                 // Preenchido em runtime
}
```

---

## 4. Payload Mínimo Obrigatório

✅ **TODOS OS CAMPOS OBRIGATÓRIOS PRESENTES**

| Campo | Tipo | V1 | V2 | Descrição |
|-------|------|----|----|-----------|
| `diagramProcessingJobId` | Guid | ✅ | ✅ | ID gerado localmente ao criar job |
| `diagramAnalysisProcessId` | Guid | ✅ | ✅ | ID do processo de análise (origem UploadOrquestracao) |
| `correlationId` | string | ✅ | ✅ | ID de correlação propagado |
| `attemptNumber` | int | ✅ | ✅ | Número sequencial de tentativa |
| `processingStatus` | string | ❌ | ✅ | "Completed" ou "Failed" |
| `rawAiOutput` | string | ❌ | ✅ | Saída bruta do modelo de IA |
| `occurredAtUtc` | DateTime | ❌ | ✅ | Timestamp em UTC |
| `trace.messageId` | Guid | ❌ | ✅ | ID único da mensagem |
| `trace.producerService` | string | ❌ | ✅ | Identificação do produtor |
| `trace.producerVersion` | string | ❌ | ✅ | Versão da aplicação |

**Teste de Contrato**: [VersionedEventContractsTests.cs](tests/ProcessadorDiagramas.ProcessingService.Tests/Application/VersionedEventContractsTests.cs)

---

## 5. AnalysisProcessingCompletedV2 vs Fallback

✅ **IMPLEMENTAÇÃO DUAL - V2 CANÔNICO COM FALLBACK V1**

**Estratégia Atual**:
- Por padrão, ambos V1 e V2 são publicados simultaneamente
- Se a publicação de V2 falhar, a falha é **logada mas não marca job como falho**
- Job permanece em estado `Completed` mesmo com falha de publicação V2
- V1 fica como fallback para serviços que ainda não implementaram V2

**Código**: [ProcessDiagramProcessingJobCommandHandler.PublishCompletedV2Async](src/ProcessadorDiagramas.ProcessingService.Application/Commands/ProcessDiagramProcessingJob/ProcessDiagramProcessingJobCommandHandler.cs#L189)

**Feature Flag**: `Messaging.PublishCompletedV2Enabled` pode desabilitar V2 sem quebrar serviço

---

## 6. Endpoint Interno de Consulta por AnalysisProcessId

✅ **IMPLEMENTADO** - Para fallback do ReportingService

### 6.1 EndPoint Novo

```
GET /internal/jobs/analysis/{analysisProcessId:guid}
```

**Resposta (200 OK)**:
```json
{
  "id": "{Guid}",
  "diagramAnalysisProcessId": "{Guid}",
  "correlationId": "{string}",
  "status": "Completed|InProgress|Failed|Pending",
  "inputStorageKey": "{string}",
  "preprocessedContent": "{string|null}",
  "rawAiOutput": "{string|null}",
  "failureReason": "{string|null}",
  "startedAt": "{DateTime:UTC|null}",
  "completedAt": "{DateTime:UTC|null}",
  "createdAt": "{DateTime:UTC}",
  "updatedAt": "{DateTime:UTC|null}"
}
```

**Resposta (404 Not Found)**:
```json
{
  "error": "Job not found for the given analysis process id."
}
```

### 6.2 Casos de Uso

- ✅ ReportingService consulta de forma síncrona se tiver que aguardar
- ✅ Fallback para webhook falho (ReportingService pode retentar)
- ✅ Debugging e auditoria de status de processamento

### 6.3 Query Handler

[GetDiagramProcessingJobByAnalysisProcessIdQueryHandler](src/ProcessadorDiagramas.ProcessingService.Application/Queries/GetDiagramProcessingJobByAnalysisProcessId/GetDiagramProcessingJobByAnalysisProcessIdQueryHandler.cs)

---

## 7. Alinhamento de Entrada com UploadOrquestracaoService

✅ **CONTRATO DE ENTRADA CONSISTENTE**

### 7.1 AnalysisProcessRequestedEvent (Consumido)

Esperado do UploadOrquestracaoService:

```csharp
{
  "diagramAnalysisProcessId": "{Guid}",
  "inputStorageKey": "{string}",           // ex: "uploads/diagram.mmd" ou caminho S3
  "correlationId": "{string}",             // propagado de ponta a ponta
  "requestedAt": "{DateTime:UTC}"
}
```

### 7.2 Tratamento de Duplicatas

Se o mesmo `diagramAnalysisProcessId` for recebido novamente:
- ✅ Job existente é reutilizado (sem reprocessamento)
- ✅ Mensagem é tratada como duplicata
- ✅ Idempotência garantida por repositório

**Código**: [AnalysisProcessRequestedEventHandler.HandleAsync](src/ProcessadorDiagramas.ProcessingService.Application/EventHandlers/AnalysisProcessRequestedEventHandler.cs)

---

## 8. Testes Abrangentes

✅ **COBERTURA COMPLETA - 43 Testes Passando**

### 8.1 Novos Testes Adicionados

| Teste | Coverage |
|-------|----------|
| `GetDiagramProcessingJobByAnalysisProcessIdQueryHandlerTests::WhenJobExistsWithResult_ReturnsMappedResponseWithRawAiOutput` | ✅ Retorna job + resultado |
| `GetDiagramProcessingJobByAnalysisProcessIdQueryHandlerTests::WhenJobExistsButNotCompleted_ReturnsResponseWithoutRawAiOutput` | ✅ Sem resultado se não concluído |
| `GetDiagramProcessingJobByAnalysisProcessIdQueryHandlerTests::WhenJobExistsButFailed_ReturnsResponseWithFailureReason` | ✅ Retorna motivo da falha |
| `GetDiagramProcessingJobByAnalysisProcessIdQueryHandlerTests::WhenJobDoesNotExist_ReturnsNull` | ✅ 404 interno |

### 8.2 Testes Existentes Mantidos

- ✅ [ProcessDiagramProcessingJobCommandHandlerTests](tests/ProcessadorDiagramas.ProcessingService.Tests/Application/ProcessDiagramProcessingJobCommandHandlerTests.cs) - 5 testes
- ✅ [AnalysisProcessRequestedEventHandlerTests](tests/ProcessadorDiagramas.ProcessingService.Tests/Application/AnalysisProcessRequestedEventHandlerTests.cs) - 2 testes
- ✅ [VersionedEventContractsTests](tests/ProcessadorDiagramas.ProcessingService.Tests/Application/VersionedEventContractsTests.cs) - 2 testes
- ✅ Domain, Infrastructure e Lambda tests

**Execução**:
```bash
dotnet test ProcessadorDiagramas.ProcessingService.sln
# Result: Passed! 43 tests
```

---

## 9. Documentação de Configuração

### 9.1 appsettings.json

```json
{
  "Messaging": {
    "PublishCompletedV2Enabled": true,
    "PublishFailedV2Enabled": true,
    "ProducerService": "ProcessadorDiagramas.ProcessingService",
    "ProducerVersion": ""
  },
  "DiagramSourceStorage": {
    "Provider": "Local",                    // "Local" ou "Aws"
    "BucketName": "",                       // S3 bucket se Provider=Aws
    "KeyPrefix": ""                         // Prefixo opcional
  },
  "AiProvider": {
    "Enabled": false,                       // true = usar real, false = Dummy
    "Provider": "Dummy",                    // "Dummy" ou "OpenAiCompatible"
    "BaseUrl": "https://api.openai.com/",
    "ApiKey": "",
    "Model": "gpt-4o-mini"
  }
}
```

### 9.2 Variáveis de Ambiente

```bash
# AWS
AWS_REGION=us-east-1
AWS_QUEUE_URL=https://sqs.us-east-1.amazonaws.com/ACCOUNT_ID/queue-name
AWS_TOPIC_ARN=arn:aws:sns:us-east-1:ACCOUNT_ID:topic-name

# IA Provider (opcional)
AI_PROVIDER_API_KEY=sk-...
AI_PROVIDER_MODEL=gpt-4o-mini
```

---

## 10. Checklist Final de Alinhamento

- ✅ ProcessingService é worker de domínio com responsabilidades corretas
- ✅ Eventos publicados em contrato canônico (V2 com V1 fallback)
- ✅ Payload obrigatório completo em todos os eventos
- ✅ rawAiOutput presente em AnalysisProcessingCompletedV2Event
- ✅ outputHash (sha256) incluso para integridade
- ✅ trace com producerService, producerVersion, messageId
- ✅ Endpoint interno /internal/jobs/analysis/{id} implementado
- ✅ Query handler + response com resultado bruto incluído
- ✅ Entrada alinhada com UploadOrquestracaoService
- ✅ Idempotência em consumo de duplicatas
- ✅ Testes cobrem: sucesso, falha, sem resultado, não encontrado
- ✅ Feature flags para viabilizar migrações gradativas
- ✅ Documentação e configuração completas

---

## 11. Próximos Passos Opcionais

1. **Observabilidade**: Integrar com X-Ray e CloudWatch para rastrear execuções
2. **Retry Logic**: Implementar backoff exponencial para falhas transitórias
3. **Dead Letter Queue**: Capturar mensagens não processáveis
4. **Rate Limiting**: Controlar concorrência vs. recursos de IA
5. **Audit Trail**: Manter história completa de tentativas e resultados

---

**Data de Conclusão**: Maio 2026  
**Branch**: develop  
**Status**: ✅ Pronto para integração com ReportingService
