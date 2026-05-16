namespace ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;

public sealed record AnalysisCompletedEvent(
    string RequestId,
    string CorrelationId,
    string S3ArtifactBucket,
    string S3ArtifactKey,
    string Status);
