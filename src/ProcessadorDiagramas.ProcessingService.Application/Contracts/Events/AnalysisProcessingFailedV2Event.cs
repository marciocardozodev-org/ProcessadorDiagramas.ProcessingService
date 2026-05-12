using System.Text.Json.Serialization;

namespace ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;

// Versioned async failure contract consumed by downstream services.
public sealed record AnalysisProcessingFailedV2Event(
    [property: JsonPropertyName("eventVersion")] string EventVersion,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("occurredAtUtc")] DateTime OccurredAtUtc,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("diagramAnalysisProcessId")] Guid DiagramAnalysisProcessId,
    [property: JsonPropertyName("diagramProcessingJobId")] Guid DiagramProcessingJobId,
    [property: JsonPropertyName("attemptNumber")] int AttemptNumber,
    [property: JsonPropertyName("failureReason")] string FailureReason,
    [property: JsonPropertyName("failureCode")] string? FailureCode,
    [property: JsonPropertyName("trace")] EventTraceMetadata Trace);
