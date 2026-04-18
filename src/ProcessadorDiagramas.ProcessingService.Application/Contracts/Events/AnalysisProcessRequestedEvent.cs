namespace ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;

public sealed record AnalysisProcessRequestedEvent(
    Guid DiagramAnalysisProcessId,
    string InputStorageKey,
    string CorrelationId,
    DateTime RequestedAt);