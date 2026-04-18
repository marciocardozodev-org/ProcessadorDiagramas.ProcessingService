namespace ProcessadorDiagramas.ProcessingService.Application.Commands.CreateDiagramProcessingJob;

public sealed record CreateDiagramProcessingJobResponse(
    Guid Id,
    Guid DiagramAnalysisProcessId,
    string Status,
    DateTime CreatedAt);