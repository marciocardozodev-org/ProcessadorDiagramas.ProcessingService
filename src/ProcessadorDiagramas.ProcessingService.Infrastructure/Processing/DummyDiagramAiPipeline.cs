using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Processing;

public sealed class DummyDiagramAiPipeline : IDiagramAiPipeline
{
    public Task<DiagramAiPipelineResult> AnalyzeAsync(string preprocessedContent, CancellationToken cancellationToken = default)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(preprocessedContent)));

        var payload = JsonSerializer.Serialize(new
        {
            Source = "dummy-ai-pipeline",
            Summary = "Mock analysis generated for local development and pipeline wiring.",
            ContentHash = hash,
            GeneratedAt = DateTime.UtcNow
        });

        return Task.FromResult(new DiagramAiPipelineResult(payload));
    }
}