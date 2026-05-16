using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Storage;

public sealed class S3AnalysisArtifactStorage : IAnalysisArtifactStorage
{
    private readonly IAmazonS3 _s3;
    private readonly AwsSettings _awsSettings;

    public S3AnalysisArtifactStorage(IAmazonS3 s3, IOptions<AwsSettings> awsSettings)
    {
        _s3 = s3;
        _awsSettings = awsSettings.Value;
    }

    public async Task<StoredAnalysisArtifact> SaveAsync(
        Guid diagramAnalysisProcessId,
        Guid diagramProcessingJobId,
        string requestId,
        int attemptNumber,
        string rawAiOutput,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_awsSettings.ArtifactBucket))
            throw new InvalidOperationException("Aws:ArtifactBucket must be configured.");

        var safeRequestId = requestId.Trim().Replace("/", "-");
        var artifactKey = $"analysis-results/{diagramAnalysisProcessId:N}/{safeRequestId}/job-{diagramProcessingJobId:N}/attempt-{attemptNumber}.json";

        var putRequest = new PutObjectRequest
        {
            BucketName = _awsSettings.ArtifactBucket,
            Key = artifactKey,
            ContentType = "application/json",
            ContentBody = rawAiOutput
        };

        await _s3.PutObjectAsync(putRequest, cancellationToken);

        return new StoredAnalysisArtifact(_awsSettings.ArtifactBucket, artifactKey);
    }
}
