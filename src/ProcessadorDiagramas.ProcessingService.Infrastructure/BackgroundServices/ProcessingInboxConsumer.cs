using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.ProcessingService.Application.EventHandlers;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.BackgroundServices;

public sealed class ProcessingInboxConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProcessingInboxConsumer> _logger;

    public ProcessingInboxConsumer(IServiceScopeFactory scopeFactory, ILogger<ProcessingInboxConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await messageBus.SubscribeAsync(
            async (message, cancellationToken) => await HandleMessageAsync(message, cancellationToken),
            stoppingToken);
    }

    private async Task HandleMessageAsync(BusMessage busMessage, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler>();
        var handler = handlers.FirstOrDefault(current => current.EventType == busMessage.EventType);

        if (handler is null)
        {
            _logger.LogWarning("No handler registered for event type {EventType}.", busMessage.EventType);
            return;
        }

        await handler.HandleAsync(busMessage.Payload, cancellationToken);
    }
}