namespace ProcessadorDiagramas.ProcessingService.Application.Commands.ProcessDiagramProcessingJob;

public sealed record ProcessDiagramProcessingJobResponse(
    Guid JobId,
    string Status,
    Guid ResultId,
    int AttemptNumber,
    DateTime CompletedAt);