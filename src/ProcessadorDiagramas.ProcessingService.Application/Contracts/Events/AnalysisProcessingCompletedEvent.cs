namespace ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;

public sealed record AnalysisProcessingCompletedEvent(
    Guid DiagramProcessingJobId,
    Guid DiagramAnalysisProcessId,
    string CorrelationId,
    Guid ResultId,
    int AttemptNumber,
    DateTime CompletedAt);