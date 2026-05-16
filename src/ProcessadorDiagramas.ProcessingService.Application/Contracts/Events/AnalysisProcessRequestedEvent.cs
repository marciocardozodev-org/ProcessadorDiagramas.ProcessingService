namespace ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;

public sealed record AnalysisProcessRequestedEvent(
    Guid DiagramAnalysisProcessId,
    string InputStorageKey,
    string CorrelationId,
    DateTime RequestedAt,
    string? RequestId = null,
    string? S3Bucket = null,
    string? S3Key = null);