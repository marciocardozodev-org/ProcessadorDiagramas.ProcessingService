using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Data.Repositories;

public sealed class DiagramProcessingAttemptRepository : IDiagramProcessingAttemptRepository
{
    private readonly AppDbContext _context;

    public DiagramProcessingAttemptRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DiagramProcessingAttempt attempt, CancellationToken cancellationToken = default)
    {
        await _context.DiagramProcessingAttempts.AddAsync(attempt, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DiagramProcessingAttempt>> ListByJobIdAsync(Guid diagramProcessingJobId, CancellationToken cancellationToken = default)
        => await _context.DiagramProcessingAttempts
            .Where(current => current.DiagramProcessingJobId == diagramProcessingJobId)
            .OrderBy(current => current.AttemptNumber)
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(DiagramProcessingAttempt attempt, CancellationToken cancellationToken = default)
    {
        _context.DiagramProcessingAttempts.Update(attempt);
        await _context.SaveChangesAsync(cancellationToken);
    }
}