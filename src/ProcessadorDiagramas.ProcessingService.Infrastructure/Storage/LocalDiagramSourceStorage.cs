using ProcessadorDiagramas.ProcessingService.Application.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Storage;

public sealed class LocalDiagramSourceStorage : IDiagramSourceStorage
{
    public async Task<StoredDiagramSource> ReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key cannot be empty.", nameof(storageKey));

        if (!File.Exists(storageKey))
            throw new FileNotFoundException($"Diagram file was not found at '{storageKey}'.", storageKey);

        var content = await File.ReadAllBytesAsync(storageKey, cancellationToken);
        var fileName = Path.GetFileName(storageKey);
        var contentType = ResolveContentType(Path.GetExtension(fileName));

        return new StoredDiagramSource(storageKey, fileName, contentType, content);
    }

    private static string ResolveContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".pdf" => "application/pdf",
        ".svg" => "image/svg+xml",
        ".json" => "application/json",
        ".xml" or ".drawio" => "application/xml",
        _ => "text/plain"
    };
}