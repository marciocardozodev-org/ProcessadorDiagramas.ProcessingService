using ProcessadorDiagramas.ProcessingService.Domain.Entities;

namespace ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

public interface IDiagramProcessingJobRepository
{
    Task AddAsync(DiagramProcessingJob job, CancellationToken cancellationToken = default);

    Task<DiagramProcessingJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DiagramProcessingJob?> GetByDiagramAnalysisProcessIdAsync(Guid diagramAnalysisProcessId, CancellationToken cancellationToken = default);

    Task UpdateAsync(DiagramProcessingJob job, CancellationToken cancellationToken = default);
}