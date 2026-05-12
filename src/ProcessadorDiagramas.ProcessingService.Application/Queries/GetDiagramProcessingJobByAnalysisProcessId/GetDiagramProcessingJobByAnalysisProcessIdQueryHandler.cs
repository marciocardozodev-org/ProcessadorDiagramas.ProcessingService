using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Application.Queries.GetDiagramProcessingJobByAnalysisProcessId;

public sealed class GetDiagramProcessingJobByAnalysisProcessIdQueryHandler
{
    private readonly IDiagramProcessingJobRepository _jobRepository;
    private readonly IDiagramProcessingResultRepository _resultRepository;

    public GetDiagramProcessingJobByAnalysisProcessIdQueryHandler(
        IDiagramProcessingJobRepository jobRepository,
        IDiagramProcessingResultRepository resultRepository)
    {
        _jobRepository = jobRepository;
        _resultRepository = resultRepository;
    }

    public async Task<GetDiagramProcessingJobByAnalysisProcessIdResponse?> HandleAsync(
        GetDiagramProcessingJobByAnalysisProcessIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByDiagramAnalysisProcessIdAsync(query.DiagramAnalysisProcessId, cancellationToken);
        if (job is null)
            return null;

        var result = await _resultRepository.GetByJobIdAsync(job.Id, cancellationToken);

        return new GetDiagramProcessingJobByAnalysisProcessIdResponse(
            Id: job.Id,
            DiagramAnalysisProcessId: job.DiagramAnalysisProcessId,
            CorrelationId: job.CorrelationId,
            Status: job.Status.ToString(),
            InputStorageKey: job.InputStorageKey,
            PreprocessedContent: job.PreprocessedContent,
            RawAiOutput: result?.RawAiOutput,
            FailureReason: job.FailureReason,
            StartedAt: job.StartedAt,
            CompletedAt: job.CompletedAt,
            CreatedAt: job.CreatedAt,
            UpdatedAt: job.UpdatedAt);
    }
}
