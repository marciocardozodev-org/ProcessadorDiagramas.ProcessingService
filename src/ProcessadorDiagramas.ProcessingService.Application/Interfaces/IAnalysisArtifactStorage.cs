namespace ProcessadorDiagramas.ProcessingService.Application.Interfaces;

public sealed record StoredAnalysisArtifact(
    string Bucket,
    string Key);

public interface IAnalysisArtifactStorage
{
    Task<StoredAnalysisArtifact> SaveAsync(
        Guid diagramAnalysisProcessId,
        Guid diagramProcessingJobId,
        string requestId,
        int attemptNumber,
        string rawAiOutput,
        CancellationToken cancellationToken = default);
}
