namespace ProcessadorDiagramas.ProcessingService.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public string RequestId { get; private set; } = string.Empty;
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage Create(string eventType, string payload, string correlationId, string requestId)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type cannot be empty.", nameof(eventType));

        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be empty.", nameof(payload));

        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("Correlation id cannot be empty.", nameof(correlationId));

        if (string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException("Request id cannot be empty.", nameof(requestId));

        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType.Trim(),
            Payload = payload,
            CorrelationId = correlationId.Trim(),
            RequestId = requestId.Trim(),
            Attempts = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkProcessed()
    {
        Attempts++;
        LastError = null;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailedAttempt(string error)
    {
        Attempts++;
        LastError = string.IsNullOrWhiteSpace(error) ? "Unknown outbox error." : error.Trim();
    }
}
