using System.Text.Json.Serialization;

namespace ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;

// Versioned async contract for Reporting to generate reports without HTTP lookups.
public sealed record AnalysisProcessingCompletedV2Event(
    [property: JsonPropertyName("eventVersion")] string EventVersion,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("occurredAtUtc")] DateTime OccurredAtUtc,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("diagramAnalysisProcessId")] Guid DiagramAnalysisProcessId,
    [property: JsonPropertyName("diagramProcessingJobId")] Guid DiagramProcessingJobId,
    [property: JsonPropertyName("resultId")] Guid ResultId,
    [property: JsonPropertyName("attemptNumber")] int AttemptNumber,
    [property: JsonPropertyName("processingStatus")] string ProcessingStatus,
    [property: JsonPropertyName("rawAiOutput")] string RawAiOutput,
    [property: JsonPropertyName("outputHash")] string? OutputHash,
    [property: JsonPropertyName("trace")] EventTraceMetadata Trace);
