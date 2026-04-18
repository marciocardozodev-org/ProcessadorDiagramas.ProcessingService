namespace ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;

public sealed record AnalysisProcessingFailedEvent(
    Guid DiagramProcessingJobId,
    Guid DiagramAnalysisProcessId,
    string CorrelationId,
    int AttemptNumber,
    string FailureReason,
    DateTime FailedAt);