namespace ProcessadorDiagramas.ProcessingService.Application.Queries.GetDiagramProcessingJobByAnalysisProcessId;

public sealed record GetDiagramProcessingJobByAnalysisProcessIdResponse(
    Guid Id,
    Guid DiagramAnalysisProcessId,
    string CorrelationId,
    string Status,
    string? InputStorageKey,
    string? PreprocessedContent,
    string? RawAiOutput,
    string? FailureReason,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
