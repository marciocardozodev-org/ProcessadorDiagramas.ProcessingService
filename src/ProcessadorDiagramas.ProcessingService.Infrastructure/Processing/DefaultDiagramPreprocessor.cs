using System.Text;
using System.Text.Json;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Processing;

public sealed class DefaultDiagramPreprocessor : IDiagramPreprocessor
{
    private static readonly HashSet<string> TextExtensions =
    [
        ".txt",
        ".puml",
        ".plantuml",
        ".mmd",
        ".json",
        ".xml",
        ".svg",
        ".drawio",
        ".yaml",
        ".yml"
    ];

    public Task<string> PreprocessAsync(StoredDiagramSource source, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(source.FileName);
        var isText = TextExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        var detectedFormat = ResolveDetectedFormat(extension, source.ContentType);
        var inputKind = ResolveInputKind(source.ContentType, isText);

        var payload = new
        {
            source.StorageKey,
            source.FileName,
            source.ContentType,
            InputKind = inputKind,
            DetectedFormat = detectedFormat,
            ByteSize = source.Content.Length,
            ContentEncoding = isText ? "text" : "base64",
            Content = isText
                ? Encoding.UTF8.GetString(source.Content)
                : Convert.ToBase64String(source.Content)
        };

        return Task.FromResult(JsonSerializer.Serialize(payload));
    }

    private static string ResolveDetectedFormat(string extension, string contentType)
    {
        if (!string.IsNullOrWhiteSpace(extension))
            return extension.TrimStart('.').ToLowerInvariant();

        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return "pdf";

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return contentType["image/".Length..].ToLowerInvariant();

        return "plain";
    }

    private static string ResolveInputKind(string contentType, bool isText)
    {
        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return "document";

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return "image";

        return isText ? "diagram-text" : "binary";
    }
}