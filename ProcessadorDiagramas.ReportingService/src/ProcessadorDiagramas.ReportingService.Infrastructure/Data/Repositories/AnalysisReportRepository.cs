using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.ReportingService.Domain.Entities;
using ProcessadorDiagramas.ReportingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ReportingService.Infrastructure.Data.Repositories;

public sealed class AnalysisReportRepository : IAnalysisReportRepository
{
    private readonly AppDbContext _dbContext;

    public AnalysisReportRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AnalysisReport?> GetByAnalysisProcessIdAsync(
        Guid analysisProcessId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AnalysisReports
            .FirstOrDefaultAsync(r => r.AnalysisProcessId == analysisProcessId, cancellationToken);
    }

    public async Task<AnalysisReport?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AnalysisReports
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task AddAsync(AnalysisReport report, CancellationToken cancellationToken = default)
    {
        await _dbContext.AnalysisReports.AddAsync(report, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AnalysisReport report, CancellationToken cancellationToken = default)
    {
        _dbContext.AnalysisReports.Update(report);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
