using ProcessadorDiagramas.ReportingService.Domain.Entities;

namespace ProcessadorDiagramas.ReportingService.Domain.Interfaces;

public interface IAnalysisReportRepository
{
    Task<AnalysisReport?> GetByAnalysisProcessIdAsync(Guid analysisProcessId, CancellationToken cancellationToken = default);
    Task<AnalysisReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(AnalysisReport report, CancellationToken cancellationToken = default);
    Task UpdateAsync(AnalysisReport report, CancellationToken cancellationToken = default);
}
