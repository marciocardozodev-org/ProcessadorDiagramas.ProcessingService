using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Application.Commands.CreateDiagramProcessingJob;

public sealed class CreateDiagramProcessingJobCommandHandler
{
    private readonly IDiagramProcessingJobRepository _repository;

    public CreateDiagramProcessingJobCommandHandler(IDiagramProcessingJobRepository repository)
    {
        _repository = repository;
    }

    public async Task<CreateDiagramProcessingJobResponse> HandleAsync(
        CreateDiagramProcessingJobCommand command,
        CancellationToken cancellationToken = default)
    {
        var existingJob = await _repository.GetByDiagramAnalysisProcessIdAsync(command.DiagramAnalysisProcessId, cancellationToken);
        if (existingJob is not null)
            throw new InvalidOperationException($"A processing job already exists for analysis process id {command.DiagramAnalysisProcessId}.");

        var job = DiagramProcessingJob.Create(
            command.DiagramAnalysisProcessId,
            command.InputStorageKey,
            command.CorrelationId);

        await _repository.AddAsync(job, cancellationToken);

        return new CreateDiagramProcessingJobResponse(
            job.Id,
            job.DiagramAnalysisProcessId,
            job.Status.ToString(),
            job.CreatedAt);
    }
}