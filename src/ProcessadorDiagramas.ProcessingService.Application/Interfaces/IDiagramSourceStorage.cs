namespace ProcessadorDiagramas.ProcessingService.Application.Interfaces;

public sealed record StoredDiagramSource(
    string StorageKey,
    string FileName,
    string ContentType,
    byte[] Content);

public interface IDiagramSourceStorage
{
    Task<StoredDiagramSource> ReadAsync(string storageKey, CancellationToken cancellationToken = default);
}