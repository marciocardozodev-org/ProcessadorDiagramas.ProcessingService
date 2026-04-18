using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Application.Queries.GetDiagramProcessingJob;

public sealed class GetDiagramProcessingJobQueryHandler
{
    private readonly IDiagramProcessingJobRepository _repository;

    public GetDiagramProcessingJobQueryHandler(IDiagramProcessingJobRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetDiagramProcessingJobResponse?> HandleAsync(
        GetDiagramProcessingJobQuery query,
        CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetByIdAsync(query.Id, cancellationToken);
        if (job is null)
            return null;

        return new GetDiagramProcessingJobResponse(
            job.Id,
            job.DiagramAnalysisProcessId,
            job.InputStorageKey,
            job.PreprocessedContent,
            job.Status.ToString(),
            job.StartedAt,
            job.CompletedAt,
            job.FailureReason,
            job.CorrelationId,
            job.CreatedAt,
            job.UpdatedAt);
    }
}