namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Storage;

public sealed class DiagramSourceStorageSettings
{
    public string Provider { get; set; } = "Local";

    public string BucketName { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = string.Empty;
}
