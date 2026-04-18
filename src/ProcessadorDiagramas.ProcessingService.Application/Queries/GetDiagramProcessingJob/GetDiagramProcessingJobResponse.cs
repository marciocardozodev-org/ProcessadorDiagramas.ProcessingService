namespace ProcessadorDiagramas.ProcessingService.Application.Queries.GetDiagramProcessingJob;

public sealed record GetDiagramProcessingJobResponse(
    Guid Id,
    Guid DiagramAnalysisProcessId,
    string InputStorageKey,
    string? PreprocessedContent,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? FailureReason,
    string CorrelationId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);