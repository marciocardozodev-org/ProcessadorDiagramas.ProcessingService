using ProcessadorDiagramas.ProcessingService.Domain.Entities;

namespace ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

public interface IDiagramProcessingResultRepository
{
    Task AddAsync(DiagramProcessingResult result, CancellationToken cancellationToken = default);

    Task<DiagramProcessingResult?> GetByJobIdAsync(Guid diagramProcessingJobId, CancellationToken cancellationToken = default);
}