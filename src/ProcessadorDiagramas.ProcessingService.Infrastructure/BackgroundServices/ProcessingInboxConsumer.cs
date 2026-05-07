using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

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
        using var outerScope = _scopeFactory.CreateScope();
        var messageBus = outerScope.ServiceProvider.GetRequiredService<IMessageBus>();

        await messageBus.SubscribeAsync(async (message, cancellationToken) =>
        {
            using var innerScope = _scopeFactory.CreateScope();
            var dispatcher = innerScope.ServiceProvider.GetRequiredService<MessageDispatcher>();
            await dispatcher.DispatchAsync(message, cancellationToken);
        }, stoppingToken);
    }

}