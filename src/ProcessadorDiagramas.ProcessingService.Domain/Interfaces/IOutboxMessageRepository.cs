using ProcessadorDiagramas.ProcessingService.Domain.Entities;

namespace ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OutboxMessage>> ListPendingAsync(int maxCount, CancellationToken cancellationToken = default);

    Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
