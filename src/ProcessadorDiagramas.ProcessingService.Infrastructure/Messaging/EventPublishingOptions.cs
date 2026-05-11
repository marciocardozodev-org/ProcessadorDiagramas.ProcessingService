using System.Reflection;
using Microsoft.Extensions.Options;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

public sealed class EventPublishingOptions : IEventPublishingOptions
{
    private readonly MessagingSettings _settings;

    public EventPublishingOptions(IOptions<MessagingSettings> settings)
    {
        _settings = settings.Value;
    }

    public bool PublishCompletedV2Enabled => _settings.PublishCompletedV2Enabled;

    public bool PublishFailedV2Enabled => _settings.PublishFailedV2Enabled;

    public string ProducerService => string.IsNullOrWhiteSpace(_settings.ProducerService)
        ? "ProcessadorDiagramas.ProcessingService"
        : _settings.ProducerService;

    public string ProducerVersion
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_settings.ProducerVersion))
                return _settings.ProducerVersion;

            return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        }
    }
}
