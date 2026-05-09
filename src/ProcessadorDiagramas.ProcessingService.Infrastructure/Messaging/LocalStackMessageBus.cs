using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

/// <summary>
/// LocalStack variant that keeps the same runtime-safe behavior as AwsMessageBus
/// (publish/receive/delete only) while allowing explicit DI selection in dev.
/// </summary>
public sealed class LocalStackMessageBus : IMessageBus
{
    private readonly AwsMessageBus _inner;
    private readonly ILogger<LocalStackMessageBus> _logger;

    public LocalStackMessageBus(
        IAmazonSimpleNotificationService sns,
        IAmazonSQS sqs,
        IOptions<AwsSettings> settings,
        ILogger<AwsMessageBus> awsMessageBusLogger,
        ILogger<LocalStackMessageBus> logger)
    {
        _inner = new AwsMessageBus(sns, sqs, settings, awsMessageBusLogger);
        _logger = logger;
    }

    public Task PublishAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        return _inner.PublishAsync(eventType, payload, cancellationToken);
    }

    public Task SubscribeAsync(Func<BusMessage, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("LocalStackMessageBus active: runtime broker operations are limited to publish/receive/delete.");
        return _inner.SubscribeAsync(handler, cancellationToken);
    }
}
