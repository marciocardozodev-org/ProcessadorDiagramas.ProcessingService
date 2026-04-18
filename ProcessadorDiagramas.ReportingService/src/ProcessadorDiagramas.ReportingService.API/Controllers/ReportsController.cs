using Microsoft.AspNetCore.Mvc;
using ProcessadorDiagramas.ReportingService.Application.Commands.RegenerateAnalysisReport;
using ProcessadorDiagramas.ReportingService.Application.Queries.GetOrGenerateAnalysisReport;

namespace ProcessadorDiagramas.ReportingService.API.Controllers;

/// <summary>
/// Endpoints internos de relatórios de análise de diagramas.
/// Consumidos pelo API Gateway / BFF.
/// </summary>
[ApiController]
[Route("internal/reports")]
[Produces("application/json")]
public sealed class ReportsController : ControllerBase
{
    private readonly GetOrGenerateAnalysisReportQueryHandler _getOrGenerateHandler;
    private readonly RegenerateAnalysisReportCommandHandler _regenerateHandler;

    public ReportsController(
        GetOrGenerateAnalysisReportQueryHandler getOrGenerateHandler,
        RegenerateAnalysisReportCommandHandler regenerateHandler)
    {
        _getOrGenerateHandler = getOrGenerateHandler;
        _regenerateHandler = regenerateHandler;
    }

    /// <summary>
    /// Retorna o relatório técnico estruturado para o processo de análise informado.
    /// Se o relatório ainda não foi gerado, tenta gerá-lo sob demanda consultando o ProcessingService.
    /// </summary>
    /// <param name="analysisProcessId">ID do processo de análise.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Relatório disponível (gerado ou recuperado do cache).</response>
    /// <response code="202">Relatório ainda não disponível — processamento em andamento.</response>
    /// <response code="404">Processo de análise não encontrado no ProcessingService.</response>
    [HttpGet("{analysisProcessId:guid}")]
    [ProducesResponseType(typeof(GetOrGenerateAnalysisReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GetOrGenerateAnalysisReportResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReport(
        Guid analysisProcessId,
        CancellationToken cancellationToken)
    {
        var result = await _getOrGenerateHandler.HandleAsync(
            new GetOrGenerateAnalysisReportQuery(analysisProcessId), cancellationToken);

        if (result is null)
            return NotFound(new { message = $"Analysis process '{analysisProcessId}' not found." });

        if (result.Status == "Pending")
            return Accepted(result);

        return Ok(result);
    }

    /// <summary>
    /// Força a regeneração do relatório, incrementando a versão.
    /// Útil quando o BFF detecta que o processamento foi refeito e quer um relatório atualizado.
    /// </summary>
    /// <param name="analysisProcessId">ID do processo de análise.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Relatório regenerado com sucesso.</response>
    /// <response code="202">Processamento ainda não concluído — relatório não pôde ser gerado agora.</response>
    /// <response code="404">Processo de análise não encontrado no ProcessingService.</response>
    [HttpPost("{analysisProcessId:guid}/generate")]
    [ProducesResponseType(typeof(GetOrGenerateAnalysisReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GetOrGenerateAnalysisReportResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateReport(
        Guid analysisProcessId,
        CancellationToken cancellationToken)
    {
        var result = await _regenerateHandler.HandleAsync(
            new RegenerateAnalysisReportCommand(analysisProcessId), cancellationToken);

        if (result is null)
            return NotFound(new { message = $"Analysis process '{analysisProcessId}' not found." });

        if (result.Status == "Pending")
            return Accepted(result);

        return Ok(result);
    }
}
