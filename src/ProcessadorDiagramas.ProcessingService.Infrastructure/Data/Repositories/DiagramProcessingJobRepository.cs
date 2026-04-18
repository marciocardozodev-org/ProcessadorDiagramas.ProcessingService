using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Data.Repositories;

public sealed class DiagramProcessingJobRepository : IDiagramProcessingJobRepository
{
    private readonly AppDbContext _context;

    public DiagramProcessingJobRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DiagramProcessingJob job, CancellationToken cancellationToken = default)
    {
        await _context.DiagramProcessingJobs.AddAsync(job, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DiagramProcessingJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.DiagramProcessingJobs.FirstOrDefaultAsync(current => current.Id == id, cancellationToken);

    public async Task<DiagramProcessingJob?> GetByDiagramAnalysisProcessIdAsync(Guid diagramAnalysisProcessId, CancellationToken cancellationToken = default)
        => await _context.DiagramProcessingJobs.FirstOrDefaultAsync(current => current.DiagramAnalysisProcessId == diagramAnalysisProcessId, cancellationToken);

    public async Task UpdateAsync(DiagramProcessingJob job, CancellationToken cancellationToken = default)
    {
        _context.DiagramProcessingJobs.Update(job);
        await _context.SaveChangesAsync(cancellationToken);
    }
}