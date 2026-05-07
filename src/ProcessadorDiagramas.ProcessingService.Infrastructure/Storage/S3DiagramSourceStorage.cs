using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Storage;

public sealed class S3DiagramSourceStorage : IDiagramSourceStorage
{
    private readonly IAmazonS3 _s3;
    private readonly DiagramSourceStorageSettings _settings;

    public S3DiagramSourceStorage(IAmazonS3 s3, IOptions<DiagramSourceStorageSettings> settings)
    {
        _s3 = s3;
        _settings = settings.Value;
    }

    public async Task<StoredDiagramSource> ReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key cannot be empty.", nameof(storageKey));

        if (string.IsNullOrWhiteSpace(_settings.BucketName))
            throw new InvalidOperationException("DiagramSourceStorage:BucketName must be configured for S3 provider.");

        var objectKey = ResolveObjectKey(storageKey);
        using var response = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = objectKey
        }, cancellationToken);

        await using var responseStream = response.ResponseStream;
        await using var memory = new MemoryStream();
        await responseStream.CopyToAsync(memory, cancellationToken);

        var fileName = Path.GetFileName(objectKey);
        var contentType = string.IsNullOrWhiteSpace(response.Headers.ContentType)
            ? ResolveContentType(Path.GetExtension(fileName))
            : response.Headers.ContentType;

        return new StoredDiagramSource(objectKey, fileName, contentType, memory.ToArray());
    }

    private string ResolveObjectKey(string storageKey)
    {
        var normalizedStorageKey = storageKey.TrimStart('/');

        if (string.IsNullOrWhiteSpace(_settings.KeyPrefix))
            return normalizedStorageKey;

        var prefix = _settings.KeyPrefix.Trim('/');
        if (normalizedStorageKey.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
            return normalizedStorageKey;

        return $"{prefix}/{normalizedStorageKey}";
    }

    private static string ResolveContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".pdf" => "application/pdf",
        ".svg" => "image/svg+xml",
        ".json" => "application/json",
        ".xml" or ".drawio" => "application/xml",
        ".mmd" => "text/plain",
        _ => "application/octet-stream"
    };
}
