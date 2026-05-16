using ProcessadorDiagramas.ProcessingService.Application.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Storage;

public sealed class DummyAnalysisArtifactStorage : IAnalysisArtifactStorage
{
    public Task<StoredAnalysisArtifact> SaveAsync(
        Guid diagramAnalysisProcessId,
        Guid diagramProcessingJobId,
        string requestId,
        int attemptNumber,
        string rawAiOutput,
        CancellationToken cancellationToken = default)
    {
        var key = $"mock/analysis-results/{diagramAnalysisProcessId:N}/job-{diagramProcessingJobId:N}/attempt-{attemptNumber}.json";
        return Task.FromResult(new StoredAnalysisArtifact("mock-mode", key));
    }
}
