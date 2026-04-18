using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

public sealed class DummyMessageBus : IMessageBus
{
    private readonly ILogger<DummyMessageBus> _logger;

    public DummyMessageBus(ILogger<DummyMessageBus> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DummyMessageBus publishing event {EventType}. No external broker call executed.",
            eventType);

        return Task.CompletedTask;
    }

    public Task SubscribeAsync(Func<BusMessage, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DummyMessageBus subscription enabled. No external broker configured.");
        return Task.CompletedTask;
    }
}