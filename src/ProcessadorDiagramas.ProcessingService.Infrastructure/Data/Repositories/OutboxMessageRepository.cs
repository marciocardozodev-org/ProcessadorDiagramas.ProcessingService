using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Data.Repositories;

public sealed class OutboxMessageRepository : IOutboxMessageRepository
{
    private readonly AppDbContext _context;

    public OutboxMessageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _context.OutboxMessages.AddAsync(message, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<OutboxMessage>> ListPendingAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var count = Math.Clamp(maxCount, 1, 200);
        return await _context.OutboxMessages
            .Where(x => x.ProcessedAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _context.OutboxMessages.Update(message);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
