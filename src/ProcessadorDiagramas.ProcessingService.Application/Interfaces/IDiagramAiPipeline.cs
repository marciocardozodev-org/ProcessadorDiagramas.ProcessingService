namespace ProcessadorDiagramas.ProcessingService.Application.Interfaces;

public sealed record DiagramAiPipelineResult(string RawOutput);

public interface IDiagramAiPipeline
{
    Task<DiagramAiPipelineResult> AnalyzeAsync(string preprocessedContent, CancellationToken cancellationToken = default);
}