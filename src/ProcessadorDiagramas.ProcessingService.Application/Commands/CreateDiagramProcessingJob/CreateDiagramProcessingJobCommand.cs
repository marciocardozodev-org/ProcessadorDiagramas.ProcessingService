namespace ProcessadorDiagramas.ProcessingService.Application.Commands.CreateDiagramProcessingJob;

public sealed record CreateDiagramProcessingJobCommand(
    Guid DiagramAnalysisProcessId,
    string InputStorageKey,
    string CorrelationId);