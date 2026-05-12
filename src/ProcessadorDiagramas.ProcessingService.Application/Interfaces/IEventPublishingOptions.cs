namespace ProcessadorDiagramas.ProcessingService.Application.Interfaces;

public interface IEventPublishingOptions
{
    bool PublishCompletedV2Enabled { get; }

    bool PublishFailedV2Enabled { get; }

    string ProducerService { get; }

    string ProducerVersion { get; }
}
