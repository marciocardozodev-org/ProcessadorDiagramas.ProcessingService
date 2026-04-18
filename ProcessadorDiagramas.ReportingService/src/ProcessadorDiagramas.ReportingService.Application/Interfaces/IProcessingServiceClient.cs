using ProcessadorDiagramas.ReportingService.Application.Contracts;

namespace ProcessadorDiagramas.ReportingService.Application.Interfaces;

public interface IProcessingServiceClient
{
    /// <summary>
    /// Busca o job de processamento (e seu resultado bruto de IA) pelo ID do processo de análise.
    /// Retorna null se o processo não for encontrado no ProcessingService.
    /// </summary>
    Task<ProcessingJobResult?> GetJobByAnalysisProcessIdAsync(
        Guid analysisProcessId,
        CancellationToken cancellationToken = default);
}
