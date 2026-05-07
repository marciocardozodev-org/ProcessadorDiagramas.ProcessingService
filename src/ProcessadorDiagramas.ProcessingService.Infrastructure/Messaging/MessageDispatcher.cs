using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.ProcessingService.Application.EventHandlers;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

public sealed class MessageDispatcher
{
    private readonly IEnumerable<IEventHandler> _handlers;
    private readonly ILogger<MessageDispatcher> _logger;

    public MessageDispatcher(IEnumerable<IEventHandler> handlers, ILogger<MessageDispatcher> logger)
    {
        _handlers = handlers;
        _logger = logger;
    }

    public async Task DispatchAsync(BusMessage busMessage, CancellationToken cancellationToken = default)
    {
        var scopeData = BuildScopeData(busMessage);
        using var _ = _logger.BeginScope(scopeData);

        var handler = _handlers.FirstOrDefault(h => h.EventType == busMessage.EventType);

        if (handler is null)
        {
            _logger.LogWarning("No handler registered for event type {EventType}.", busMessage.EventType);
            return;
        }

        await handler.HandleAsync(busMessage.Payload, cancellationToken);
    }

    private static Dictionary<string, object?> BuildScopeData(BusMessage busMessage)
    {
        var scopeData = new Dictionary<string, object?>
        {
            ["event_id"] = busMessage.MessageId,
            ["event_type"] = busMessage.EventType
        };

        try
        {
            using var payloadDocument = JsonDocument.Parse(busMessage.Payload);
            var payload = payloadDocument.RootElement;

            if (TryGetString(payload, "CorrelationId", out var correlationId) || TryGetString(payload, "correlationId", out correlationId))
                scopeData["correlation_id"] = correlationId;

            if (TryGetString(payload, "DiagramAnalysisProcessId", out var diagramAnalysisProcessId) || TryGetString(payload, "diagramAnalysisProcessId", out diagramAnalysisProcessId))
                scopeData["diagram_analysis_process_id"] = diagramAnalysisProcessId;

            if (TryGetString(payload, "DiagramProcessingJobId", out var jobId) || TryGetString(payload, "diagramProcessingJobId", out jobId))
                scopeData["job_id"] = jobId;
        }
        catch
        {
            // Scope enrichment is best-effort only.
        }

        return scopeData;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }
}
