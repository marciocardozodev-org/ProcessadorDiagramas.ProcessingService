using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;
using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

public sealed class OutboxPublisher : IOutboxPublisher
{
    private readonly IOutboxMessageRepository _outboxRepository;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(
        IOutboxMessageRepository outboxRepository,
        IMessageBus messageBus,
        ILogger<OutboxPublisher> logger)
    {
        _outboxRepository = outboxRepository;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _outboxRepository.ListPendingAsync(20, cancellationToken);

        foreach (var message in pending)
        {
            try
            {
                await _messageBus.PublishAsync(message.EventType, message.Payload, cancellationToken);
                message.MarkProcessed();
                await _outboxRepository.UpdateAsync(message, cancellationToken);

                _logger.LogInformation(
                    "Published outbox message. outboxId={OutboxId} eventType={EventType} requestId={RequestId} correlationId={CorrelationId}",
                    message.Id,
                    message.EventType,
                    message.RequestId,
                    message.CorrelationId);
            }
            catch (Exception ex)
            {
                message.MarkFailedAttempt(ex.Message);
                await _outboxRepository.UpdateAsync(message, cancellationToken);

                _logger.LogError(
                    ex,
                    "Failed to publish outbox message. outboxId={OutboxId} eventType={EventType} requestId={RequestId} correlationId={CorrelationId} attempts={Attempts}",
                    message.Id,
                    message.EventType,
                    message.RequestId,
                    message.CorrelationId,
                    message.Attempts);

                throw;
            }
        }
    }
}
