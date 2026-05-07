using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.TestUtilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.ProcessingService.API;
using ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;
using ProcessadorDiagramas.ProcessingService.Application.EventHandlers;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

namespace ProcessadorDiagramas.ProcessingService.Tests.Infrastructure.Lambda;

public sealed class LambdaFunctionTests
{
    [Fact]
    public async Task HandleAsync_WhenSqsEventContainsKnownEvent_DispatchesToMatchingHandler()
    {
        var spyHandler = new SpyEventHandler(nameof(AnalysisProcessRequestedEvent));
        var services = BuildServiceProvider(spyHandler);

        var eventPayload = JsonSerializer.Serialize(new AnalysisProcessRequestedEvent(
            Guid.NewGuid(), "uploads/diagram.png", "corr-456", DateTime.UtcNow));

        // SNS→SQS format: body contains SNS notification JSON with Message and MessageAttributes
        var snsBody = JsonSerializer.Serialize(new
        {
            Type = "Notification",
            MessageId = Guid.NewGuid().ToString("N"),
            Message = eventPayload,
            MessageAttributes = new
            {
                eventType = new { Type = "String", Value = nameof(AnalysisProcessRequestedEvent) }
            }
        });

        var sqsEvent = new SQSEvent
        {
            Records =
            [
                new SQSEvent.SQSMessage
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    Body = snsBody
                }
            ]
        };

        var function = new LambdaFunction(services);
        var context = new TestLambdaContext();

        var response = await function.HandleAsync(sqsEvent, context);

        response.BatchItemFailures.Should().BeEmpty();
        spyHandler.HandledPayloads.Should().HaveCount(1);
        spyHandler.HandledPayloads[0].Should().Contain("uploads/diagram.png");
    }

    [Fact]
    public async Task HandleAsync_WhenHandlerThrows_ReturnsRecordAsBatchItemFailure()
    {
        var throwingHandler = new ThrowingEventHandler(nameof(AnalysisProcessRequestedEvent));
        var services = BuildServiceProvider(throwingHandler);

        var snsBody = JsonSerializer.Serialize(new
        {
            Type = "Notification",
            Message = "{}",
            MessageAttributes = new
            {
                eventType = new { Type = "String", Value = nameof(AnalysisProcessRequestedEvent) }
            }
        });

        var messageId = Guid.NewGuid().ToString("N");
        var sqsEvent = new SQSEvent
        {
            Records = [new SQSEvent.SQSMessage { MessageId = messageId, Body = snsBody }]
        };

        var function = new LambdaFunction(services);
        var response = await function.HandleAsync(sqsEvent, new TestLambdaContext());

        response.BatchItemFailures.Should().ContainSingle(f => f.ItemIdentifier == messageId);
    }

    private static IServiceProvider BuildServiceProvider(IEventHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IEventHandler>(handler);
        services.AddScoped<MessageDispatcher>();
        return services.BuildServiceProvider();
    }

    private sealed class SpyEventHandler : IEventHandler
    {
        public SpyEventHandler(string eventType) => EventType = eventType;
        public string EventType { get; }
        public List<string> HandledPayloads { get; } = [];

        public Task HandleAsync(string payload, CancellationToken cancellationToken = default)
        {
            HandledPayloads.Add(payload);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingEventHandler : IEventHandler
    {
        public ThrowingEventHandler(string eventType) => EventType = eventType;
        public string EventType { get; }

        public Task HandleAsync(string payload, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated processing failure.");
    }
}
