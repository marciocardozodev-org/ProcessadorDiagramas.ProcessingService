namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

public sealed class AwsSettings
{
    public string Region { get; set; } = string.Empty;

    public string TopicArn { get; set; } = string.Empty;

    public string QueueUrl { get; set; } = string.Empty;

    public string? ServiceURL { get; set; }
}