using ProcessadorDiagramas.ProcessingService.Domain.Entities;

namespace ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

public interface IDiagramProcessingAttemptRepository
{
    Task AddAsync(DiagramProcessingAttempt attempt, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DiagramProcessingAttempt>> ListByJobIdAsync(Guid diagramProcessingJobId, CancellationToken cancellationToken = default);

    Task UpdateAsync(DiagramProcessingAttempt attempt, CancellationToken cancellationToken = default);
}