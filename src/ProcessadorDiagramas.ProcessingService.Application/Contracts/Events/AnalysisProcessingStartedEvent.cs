namespace ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;

public sealed record AnalysisProcessingStartedEvent(
    Guid DiagramProcessingJobId,
    Guid DiagramAnalysisProcessId,
    string CorrelationId,
    int AttemptNumber,
    DateTime StartedAt);