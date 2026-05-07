using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;
using ProcessadorDiagramas.ProcessingService.Application.EventHandlers;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;
using ProcessadorDiagramas.ProcessingService.Infrastructure.BackgroundServices;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

namespace ProcessadorDiagramas.ProcessingService.Tests.Infrastructure.BackgroundServices;

public sealed class ProcessingInboxConsumerTests
{
    [Fact]
    public async Task HostedService_WhenBusReceivesKnownEvent_DispatchesToMatchingHandler()
    {
        var spyHandler = new SpyEventHandler(nameof(AnalysisProcessRequestedEvent));
        var messageBus = new TriggeringMessageBus(new BusMessage(
            Guid.NewGuid().ToString("N"),
            nameof(AnalysisProcessRequestedEvent),
            JsonSerializer.Serialize(new AnalysisProcessRequestedEvent(Guid.NewGuid(), "uploads/diagram.png", "corr-123", DateTime.UtcNow))));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMessageBus>(messageBus);
        services.AddSingleton<IEventHandler>(spyHandler);
        services.AddScoped<MessageDispatcher>();
        services.AddSingleton<IHostedService, ProcessingInboxConsumer>();

        await using var provider = services.BuildServiceProvider();
        var hostedService = provider.GetRequiredService<IHostedService>();

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);

        spyHandler.HandledPayloads.Should().HaveCount(1);
        spyHandler.HandledPayloads[0].Should().Contain("uploads/diagram.png");
    }

    private sealed class SpyEventHandler : IEventHandler
    {
        public SpyEventHandler(string eventType)
        {
            EventType = eventType;
        }

        public string EventType { get; }

        public List<string> HandledPayloads { get; } = [];

        public Task HandleAsync(string payload, CancellationToken cancellationToken = default)
        {
            HandledPayloads.Add(payload);
            return Task.CompletedTask;
        }
    }

    private sealed class TriggeringMessageBus : IMessageBus
    {
        private readonly BusMessage _message;

        public TriggeringMessageBus(BusMessage message)
        {
            _message = message;
        }

        public Task PublishAsync(string eventType, string payload, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SubscribeAsync(Func<BusMessage, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
            => handler(_message, cancellationToken);
    }
}