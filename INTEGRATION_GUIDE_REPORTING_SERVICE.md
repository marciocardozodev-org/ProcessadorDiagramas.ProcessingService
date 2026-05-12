# Guia de Integração: ReportingService com ProcessingService

## Quick Start para ReportingService

### 1. Eventos Publicados pelo ProcessingService

ProcessingService publica para SNS os seguintes eventos:

#### 1.1 Início do Processamento
**Evento**: `AnalysisProcessingStartedEvent`  
**Topic**: Mesmo SNS topic cadastrado em `AWS_TOPIC_ARN`

```json
{
  "diagramProcessingJobId": "uuid",
  "diagramAnalysisProcessId": "uuid",
  "correlationId": "string",
  "attemptNumber": 1,
  "startedAt": "2026-05-12T10:00:00Z"
}
```

#### 1.2 Processamento Concluído (V2 - CANÔNICO)
**Evento**: `AnalysisProcessingCompletedV2Event`  
**Topic**: Mesmo SNS topic  
**Prioridade**: Consumir este evento

```json
{
  "eventVersion": "2.0.0",
  "eventType": "AnalysisProcessingCompletedV2Event",
  "occurredAtUtc": "2026-05-12T10:05:00Z",
  "correlationId": "string",
  "diagramAnalysisProcessId": "uuid",
  "diagramProcessingJobId": "uuid",
  "resultId": "uuid",
  "attemptNumber": 1,
  "processingStatus": "Completed",
  "rawAiOutput": "{json do modelo da IA}",
  "outputHash": "sha256:abc123...",
  "trace": {
    "producerService": "ProcessadorDiagramas.ProcessingService",
    "producerVersion": "X.X.X",
    "messageId": "uuid"
  }
}
```

#### 1.3 Processamento Falhou (V2 - CANÔNICO)
**Evento**: `AnalysisProcessingFailedV2Event`  
**Topic**: Mesmo SNS topic

```json
{
  "eventVersion": "2.0.0",
  "eventType": "AnalysisProcessingFailedV2Event",
  "occurredAtUtc": "2026-05-12T10:05:00Z",
  "correlationId": "string",
  "diagramAnalysisProcessId": "uuid",
  "diagramProcessingJobId": "uuid",
  "attemptNumber": 1,
  "failureReason": "Storage read error",
  "failureCode": "STORAGE_ERROR",
  "trace": {
    "producerService": "ProcessadorDiagramas.ProcessingService",
    "producerVersion": "X.X.X",
    "messageId": "uuid"
  }
}
```

---

### 2. Endpoint Interno (Fallback)

Se o webhook do ReportingService falhar, pode usar este endpoint para retry:

```http
GET http://processingservice:5080/internal/jobs/analysis/{analysisProcessId}
```

**Resposta (200 OK)**:
```json
{
  "id": "job-uuid",
  "diagramAnalysisProcessId": "analysis-uuid",
  "correlationId": "corr-123",
  "status": "Completed",
  "inputStorageKey": "uploads/diagram.mmd",
  "preprocessedContent": "{preprocessed mermaid}",
  "rawAiOutput": "{json resultado}",
  "failureReason": null,
  "startedAt": "2026-05-12T10:00:00Z",
  "completedAt": "2026-05-12T10:05:00Z",
  "createdAt": "2026-05-12T09:59:00Z",
  "updatedAt": "2026-05-12T10:05:00Z"
}
```

**Resposta (404 Not Found)**:
```json
{
  "error": "Job not found for the given analysis process id."
}
```

---

### 3. Processando Eventos

#### 3.1 AnalysisProcessingCompletedV2Event
- Campo obrigatório: **`rawAiOutput`**
- Campo obrigatório: **`correlationId`** (para rastrear a requisição ponta a ponta)
- Campo obrigatório: **`diagramAnalysisProcessId`** (seu ID de processo)
- Use `occurredAtUtc` como timestamp do evento
- Use `trace.messageId` para deduplicação e auditoria

#### 3.2 AnalysisProcessingFailedV2Event
- Campo obrigatório: **`failureReason`** (motivo da falha)
- Campo opcional: **`failureCode`** (código estruturado, ex: "STORAGE_ERROR")
- Implementar retry com backoff exponencial se configurado
- Possibilidade de reenviar a requisição para reprocessamento

---

### 4. Estratégia de Integração Recomendada

```csharp
// A) Consumer de SQS (webhook assíncrono)
public sealed class AnalysisProcessingEventConsumer
{
    public async Task HandleCompletedV2Async(AnalysisProcessingCompletedV2Event @event)
    {
        // 1. Validar formato do rawAiOutput
        var aiResult = JsonSerializer.Deserialize<AiAnalysisResult>(@event.RawAiOutput);
        
        // 2. Gerar relatório
        var report = GenerateReport(@event.DiagramAnalysisProcessId, aiResult);
        
        // 3. Persistir relatório
        await _reportRepository.SaveAsync(report);
        
        // 4. Publicar evento de conclusão downstream
        await _messageBus.PublishAsync(nameof(ReportGeneratedEvent), report);
    }

    public async Task HandleFailedV2Async(AnalysisProcessingFailedV2Event @event)
    {
        _logger.LogWarning(
            "Processing failed for {AnalysisId}. Reason: {Reason}",
            @event.DiagramAnalysisProcessId,
            @event.FailureReason);
        
        // 1. Marcar análise como falha
        await _analysisRepository.MarkAsFailedAsync(
            @event.DiagramAnalysisProcessId,
            @event.FailureReason);
        
        // 2. Alertar usuário (se necessário)
        await _notificationService.NotifyAsync(
            @event.DiagramAnalysisProcessId,
            "Diagram processing failed");
    }
}

// B) Fallback via HTTP (se SQS falhar)
public sealed class ProcessingServiceClient
{
    private readonly HttpClient _httpClient;

    public async Task<DiagramProcessingJobResponse?> GetJobByAnalysisIdAsync(
        Guid analysisProcessId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/internal/jobs/analysis/{analysisProcessId}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<DiagramProcessingJobResponse>(json);
    }
}
```

---

### 5. Mapeamento de Campos

| ReportingService | ← | ProcessingService | Campo |
|------------------|---|---|---|
| `analysisId` | ← | `diagramAnalysisProcessId` | Seu ID de processo |
| `jobId` | ← | `diagramProcessingJobId` | ID do job de processamento |
| `correlationId` | ← | `correlationId` | Rastreamento ponta a ponta |
| `aiOutput` | ← | `rawAiOutput` | Resultado bruto do modelo |
| `processingStatus` | ← | `processingStatus` | "Completed" / "Failed" |
| `failureMessage` | ← | `failureReason` | Descrição do erro |
| `processedAt` | ← | `occurredAtUtc` | Timestamp UTC |

---

### 6. Tratamento de Erros

#### 6.1 Símbolo de Falha Transitória
Se ProcessingService não responder ou retornar erro 5xx:
- Retry com backoff exponencial (2^n segundos)
- Máximo de 3 tentativas recomendado
- Dead Letter Queue após falhas persistentes

#### 6.2 Símbolo de Desserialização
Se `rawAiOutput` não puder ser desserializado:
- Registrar como falha de validação
- Incluir payload problemático em logs para auditoria
- Notificar administrador

#### 6.3 Símbolo de Divergência de Estado
Se um evento chegar fora de ordem (ex: Completed antes de Started):
- Usar `trace.messageId` para deduplicação
- Manter histórico de eventos recebidos
- Validar transições de estado esperadas

---

### 7. Testes de Integração

```bash
# 1. Iniciar ProcessingService
docker compose up -d processingservice

# 2. Publicar AnalysisProcessRequestedEvent de teste
aws sns publish \
  --topic-arn arn:aws:sns:us-east-1:ACCOUNT:processing-topic \
  --message '{"DiagramAnalysisProcessId":"uuid","InputStorageKey":"test.mmd","CorrelationId":"corr-123","RequestedAt":"2026-05-12T10:00:00Z"}'

# 3. Aguardar e verificar AnalysisProcessingCompletedV2Event na fila SQS

# 4. Testar fallback HTTP
curl http://localhost:5080/internal/jobs/analysis/uuid-aqui
```

---

### 8. Configuração em ReportingService

```appsettings.json
{
  "Processing": {
    "ServiceUrl": "http://processingservice:5080",
    "RetryMaxAttempts": 3,
    "RetryDelayMs": 1000
  },
  "Messaging": {
    "TopicArn": "arn:aws:sns:...",
    "QueueUrl": "https://sqs...."
  }
}
```

---

### 9. Checklist de Integração

- [ ] SQS consumindo eventos do SNS
- [ ] Handler de `AnalysisProcessingCompletedV2Event` desserializando corretamente
- [ ] Validação de `rawAiOutput` como JSON válido
- [ ] Persistência de resultado em DB do ReportingService
- [ ] Fallback HTTP implementado para retry
- [ ] Testes de happy path (sucesso)
- [ ] Testes de unhappy path (falha)
- [ ] Logs incluindo `correlationId` para rastreamento
- [ ] Alertas sobre falhas persistentes
- [ ] Dead Letter Queue configurada

---

**Versão do Contrato**: 2.0.0  
**Data**: Maio 2026  
**Status**: ✅ Pronto para consumo
