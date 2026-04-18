using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.ReportingService.Application.Contracts;
using ProcessadorDiagramas.ReportingService.Application.Interfaces;

namespace ProcessadorDiagramas.ReportingService.Infrastructure.Clients;

/// <summary>
/// Client HTTP para consultar o ProcessingService e obter o resultado bruto de análise.
/// Consome o endpoint interno: GET /internal/jobs/by-analysis-process/{analysisProcessId}
/// </summary>
public sealed class ProcessingServiceClient : IProcessingServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProcessingServiceClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ProcessingServiceClient(HttpClient httpClient, ILogger<ProcessingServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ProcessingJobResult?> GetJobByAnalysisProcessIdAsync(
        Guid analysisProcessId,
        CancellationToken cancellationToken = default)
    {
        var url = $"/internal/jobs/by-analysis-process/{analysisProcessId}";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "ProcessingService returned 404 for analysis process {AnalysisProcessId}.",
                    analysisProcessId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ProcessingJobResult>(
                JsonOptions, cancellationToken);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP error while fetching processing job for analysis process {AnalysisProcessId}.",
                analysisProcessId);
            throw;
        }
    }
}
