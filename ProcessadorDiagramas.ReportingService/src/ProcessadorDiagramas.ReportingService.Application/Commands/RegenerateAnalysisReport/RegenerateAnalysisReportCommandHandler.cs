using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.ReportingService.Application.Interfaces;
using ProcessadorDiagramas.ReportingService.Application.Queries.GetOrGenerateAnalysisReport;
using ProcessadorDiagramas.ReportingService.Domain.Entities;
using ProcessadorDiagramas.ReportingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ReportingService.Application.Commands.RegenerateAnalysisReport;

/// <summary>
/// Força a regeneração do relatório, incrementando a versão.
/// Útil quando o ProcessingService atualizou o resultado e o BFF solicita uma nova versão.
/// </summary>
public sealed class RegenerateAnalysisReportCommandHandler
{
    private readonly IAnalysisReportRepository _reportRepository;
    private readonly IProcessingServiceClient _processingServiceClient;
    private readonly ILogger<RegenerateAnalysisReportCommandHandler> _logger;

    public RegenerateAnalysisReportCommandHandler(
        IAnalysisReportRepository reportRepository,
        IProcessingServiceClient processingServiceClient,
        ILogger<RegenerateAnalysisReportCommandHandler> logger)
    {
        _reportRepository = reportRepository;
        _processingServiceClient = processingServiceClient;
        _logger = logger;
    }

    public async Task<GetOrGenerateAnalysisReportResponse?> HandleAsync(
        RegenerateAnalysisReportCommand command,
        CancellationToken cancellationToken = default)
    {
        var jobResult = await _processingServiceClient.GetJobByAnalysisProcessIdAsync(
            command.AnalysisProcessId, cancellationToken);

        if (jobResult is null)
            return null;

        if (!string.Equals(jobResult.JobStatus, "Completed", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(jobResult.RawAiOutput))
        {
            _logger.LogInformation(
                "Cannot regenerate: processing job for {AnalysisProcessId} is not completed.",
                command.AnalysisProcessId);

            var existing = await _reportRepository.GetByAnalysisProcessIdAsync(
                command.AnalysisProcessId, cancellationToken);

            if (existing is null)
            {
                var pending = AnalysisReport.CreatePending(command.AnalysisProcessId);
                await _reportRepository.AddAsync(pending, cancellationToken);
                return MapToResponse(pending);
            }

            return MapToResponse(existing);
        }

        var (components, risks, recommendations) = AnalysisReportComposer.Compose(jobResult);
        var sourceRef = jobResult.JobId.ToString();

        var report = await _reportRepository.GetByAnalysisProcessIdAsync(
            command.AnalysisProcessId, cancellationToken);

        if (report is not null)
        {
            report.BumpVersion();
            report.MarkAsGenerated(components, risks, recommendations, sourceRef);
            await _reportRepository.UpdateAsync(report, cancellationToken);
        }
        else
        {
            report = AnalysisReport.CreatePending(command.AnalysisProcessId);
            report.MarkAsGenerated(components, risks, recommendations, sourceRef);
            await _reportRepository.AddAsync(report, cancellationToken);
        }

        _logger.LogInformation(
            "Report regenerated (v{Version}) for analysis process {AnalysisProcessId}.",
            report.Version, command.AnalysisProcessId);

        return MapToResponse(report);
    }

    private static GetOrGenerateAnalysisReportResponse MapToResponse(AnalysisReport r) =>
        new(r.Id, r.AnalysisProcessId, r.Status.ToString(),
            r.ComponentsSummary, r.ArchitecturalRisks, r.Recommendations,
            r.SourceAnalysisReference, r.Version, r.FailureReason,
            r.GeneratedAt, r.CreatedAt, r.UpdatedAt);
}
