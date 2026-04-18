using ProcessadorDiagramas.ReportingService.Domain.Enums;

namespace ProcessadorDiagramas.ReportingService.Application.Queries.GetOrGenerateAnalysisReport;

public sealed record GetOrGenerateAnalysisReportResponse(
    Guid ReportId,
    Guid AnalysisProcessId,
    string Status,
    string? ComponentsSummary,
    string? ArchitecturalRisks,
    string? Recommendations,
    string? SourceAnalysisReference,
    int Version,
    string? FailureReason,
    DateTime? GeneratedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
