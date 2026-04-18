using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Processing;

namespace ProcessadorDiagramas.ProcessingService.Tests.Infrastructure.Processing;

public sealed class OpenAiCompatibleDiagramAiPipelineTests
{
    [Fact]
    public async Task AnalyzeAsync_WhenProviderReturnsChatCompletion_MapsStructuredOutput()
    {
        var responsePayload = """
        {
          "id": "chatcmpl-test",
          "choices": [
            {
              "message": {
                "content": "Resumo do diagrama: fluxo validado com sucesso."
              }
            }
          ]
        }
        """;

        var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responsePayload, Encoding.UTF8, "application/json")
            }))
        {
            BaseAddress = new Uri("https://provider.test")
        };

        var pipeline = new OpenAiCompatibleDiagramAiPipeline(
            httpClient,
            Options.Create(new AiProviderSettings
            {
                Enabled = true,
                Provider = "OpenAICompatible",
                BaseUrl = "https://provider.test",
                ApiKey = "secret-key",
                Model = "gpt-4o-mini"
            }),
            NullLogger<OpenAiCompatibleDiagramAiPipeline>.Instance);

        var result = await pipeline.AnalyzeAsync("{\"FileName\":\"diagram.mmd\"}");
        using var document = JsonDocument.Parse(result.RawOutput);

        document.RootElement.GetProperty("Source").GetString().Should().Be("openai-compatible");
        document.RootElement.GetProperty("Model").GetString().Should().Be("gpt-4o-mini");
        document.RootElement.GetProperty("Summary").GetString().Should().Contain("fluxo validado");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
