namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

public sealed class MessagingSettings
{
    public bool PublishCompletedV2Enabled { get; set; } = true;

    public bool PublishFailedV2Enabled { get; set; } = true;

    public string ProducerService { get; set; } = "ProcessadorDiagramas.ProcessingService";

    public string ProducerVersion { get; set; } = string.Empty;
}
