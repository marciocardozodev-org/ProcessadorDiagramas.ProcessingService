using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.ReportingService.Application.Interfaces;
using ProcessadorDiagramas.ReportingService.Domain.Entities;
using ProcessadorDiagramas.ReportingService.Domain.Enums;
using ProcessadorDiagramas.ReportingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ReportingService.Application.Queries.GetOrGenerateAnalysisReport;

/// <summary>
/// Estratégia: relatório sob demanda com persistência (cache).
/// 1. Se relatório Generated já existe → retorna do banco (cache).
/// 2. Se não existe → busca no ProcessingService, compõe e persiste.
/// 3. Se o job ainda não completou → retorna status Pending para o caller sinalizar "ainda não disponível".
/// </summary>
public sealed class GetOrGenerateAnalysisReportQueryHandler
{
    private readonly IAnalysisReportRepository _reportRepository;
    private readonly IProcessingServiceClient _processingServiceClient;
    private readonly ILogger<GetOrGenerateAnalysisReportQueryHandler> _logger;

    public GetOrGenerateAnalysisReportQueryHandler(
        IAnalysisReportRepository reportRepository,
        IProcessingServiceClient processingServiceClient,
        ILogger<GetOrGenerateAnalysisReportQueryHandler> logger)
    {
        _reportRepository = reportRepository;
        _processingServiceClient = processingServiceClient;
        _logger = logger;
    }

    public async Task<GetOrGenerateAnalysisReportResponse?> HandleAsync(
        GetOrGenerateAnalysisReportQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Consulta cache local
        var existing = await _reportRepository.GetByAnalysisProcessIdAsync(
            query.AnalysisProcessId, cancellationToken);

        if (existing is not null && existing.Status == AnalysisReportStatus.Generated)
        {
            _logger.LogInformation(
                "Returning cached report for analysis process {AnalysisProcessId}.",
                query.AnalysisProcessId);
            return MapToResponse(existing);
        }

        // 2. Busca dados brutos no ProcessingService
        var jobResult = await _processingServiceClient.GetJobByAnalysisProcessIdAsync(
            query.AnalysisProcessId, cancellationToken);

        if (jobResult is null)
        {
            // Processo de análise não encontrado
            return null;
        }

        // 3. Job ainda não completou
        if (!string.Equals(jobResult.JobStatus, "Completed", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(jobResult.RawAiOutput))
        {
            _logger.LogInformation(
                "Processing job for analysis process {AnalysisProcessId} is not yet completed (status: {Status}).",
                query.AnalysisProcessId, jobResult.JobStatus);

            // Persiste registro Pending para rastreabilidade, se ainda não existir
            if (existing is null)
            {
                var pending = AnalysisReport.CreatePending(query.AnalysisProcessId);
                await _reportRepository.AddAsync(pending, cancellationToken);
                return MapToResponse(pending);
            }

            return MapToResponse(existing);
        }

        // 4. Composição do relatório
        AnalysisReport report;
        try
        {
            var (components, risks, recommendations) = AnalysisReportComposer.Compose(jobResult);
            var sourceRef = jobResult.JobId.ToString();

            if (existing is not null)
            {
                existing.MarkAsGenerated(components, risks, recommendations, sourceRef);
                await _reportRepository.UpdateAsync(existing, cancellationToken);
                report = existing;
            }
            else
            {
                var newReport = AnalysisReport.CreatePending(query.AnalysisProcessId);
                newReport.MarkAsGenerated(components, risks, recommendations, sourceRef);
                await _reportRepository.AddAsync(newReport, cancellationToken);
                report = newReport;
            }

            _logger.LogInformation(
                "Report generated and persisted for analysis process {AnalysisProcessId}.",
                query.AnalysisProcessId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to compose report for analysis process {AnalysisProcessId}.",
                query.AnalysisProcessId);

            if (existing is not null)
            {
                existing.MarkAsFailed(ex.Message);
                await _reportRepository.UpdateAsync(existing, cancellationToken);
                return MapToResponse(existing);
            }

            var failed = AnalysisReport.CreatePending(query.AnalysisProcessId);
            failed.MarkAsFailed(ex.Message);
            await _reportRepository.AddAsync(failed, cancellationToken);
            return MapToResponse(failed);
        }

        return MapToResponse(report);
    }

    private static GetOrGenerateAnalysisReportResponse MapToResponse(AnalysisReport r) =>
        new(r.Id, r.AnalysisProcessId, r.Status.ToString(),
            r.ComponentsSummary, r.ArchitecturalRisks, r.Recommendations,
            r.SourceAnalysisReference, r.Version, r.FailureReason,
            r.GeneratedAt, r.CreatedAt, r.UpdatedAt);
}
