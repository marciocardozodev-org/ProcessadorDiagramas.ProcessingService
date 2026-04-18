using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Processing;

public sealed class OpenAiCompatibleDiagramAiPipeline : IDiagramAiPipeline
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AiProviderSettings _settings;
    private readonly ILogger<OpenAiCompatibleDiagramAiPipeline> _logger;

    public OpenAiCompatibleDiagramAiPipeline(
        HttpClient httpClient,
        IOptions<AiProviderSettings> settings,
        ILogger<OpenAiCompatibleDiagramAiPipeline> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<DiagramAiPipelineResult> AnalyzeAsync(string preprocessedContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(preprocessedContent))
            throw new ArgumentException("Preprocessed content cannot be empty.", nameof(preprocessedContent));

        EnsureConfigured();

        var requestPayload = new
        {
            model = _settings.Model,
            temperature = _settings.Temperature,
            max_tokens = _settings.MaxTokens,
            messages = new object[]
            {
                new { role = "system", content = _settings.PromptTemplate },
                new
                {
                    role = "user",
                    content = "Analise o payload pré-processado do diagrama e devolva um resumo técnico em português com componentes, integrações, observações e possíveis riscos.\n\n" + preprocessedContent
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestPayload, RequestJsonOptions), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        var summary = ExtractSummary(responseBody);
        var responseId = ExtractResponseId(responseBody);

        _logger.LogInformation("AI provider {Provider} returned a processed response for model {Model}.", _settings.Provider, _settings.Model);

        var payload = JsonSerializer.Serialize(new
        {
            Source = "openai-compatible",
            Provider = _settings.Provider,
            Model = _settings.Model,
            Summary = summary,
            ResponseId = responseId,
            GeneratedAt = DateTime.UtcNow,
            RawResponse = responseBody
        });

        return new DiagramAiPipelineResult(payload);
    }

    private void EnsureConfigured()
    {
        if (!_settings.Enabled)
            throw new InvalidOperationException("AI provider is disabled for the current environment.");

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new InvalidOperationException("AI provider API key was not configured.");

        if (_httpClient.BaseAddress is null)
        {
            if (!Uri.TryCreate(_settings.BaseUrl, UriKind.Absolute, out var baseUri))
                throw new InvalidOperationException("AI provider base URL is invalid or missing.");

            _httpClient.BaseAddress = baseUri;
        }
    }

    private static string ExtractSummary(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);

        if (document.RootElement.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content)
                && !string.IsNullOrWhiteSpace(content.GetString()))
            {
                return content.GetString()!;
            }
        }

        if (document.RootElement.TryGetProperty("output_text", out var outputText)
            && !string.IsNullOrWhiteSpace(outputText.GetString()))
        {
            return outputText.GetString()!;
        }

        throw new InvalidOperationException("AI provider response did not include recognizable text content.");
    }

    private static string? ExtractResponseId(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }
}
