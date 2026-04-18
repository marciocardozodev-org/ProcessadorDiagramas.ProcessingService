using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Data.Repositories;

public sealed class DiagramProcessingResultRepository : IDiagramProcessingResultRepository
{
    private readonly AppDbContext _context;

    public DiagramProcessingResultRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DiagramProcessingResult result, CancellationToken cancellationToken = default)
    {
        await _context.DiagramProcessingResults.AddAsync(result, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DiagramProcessingResult?> GetByJobIdAsync(Guid diagramProcessingJobId, CancellationToken cancellationToken = default)
        => await _context.DiagramProcessingResults.FirstOrDefaultAsync(current => current.DiagramProcessingJobId == diagramProcessingJobId, cancellationToken);
}