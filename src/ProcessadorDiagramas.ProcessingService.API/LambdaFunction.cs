using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

namespace ProcessadorDiagramas.ProcessingService.API;

public sealed class LambdaFunction
{
    private readonly IServiceProvider _services;

    public LambdaFunction(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<SQSBatchResponse> HandleAsync(SQSEvent sqsEvent, ILambdaContext context)
    {
        var failures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                var payload = ExtractPayload(record.Body);
                var eventType = ResolveEventType(record);
                var busMessage = new BusMessage(record.MessageId, eventType, payload);

                using var scope = _services.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<MessageDispatcher>();
                await dispatcher.DispatchAsync(busMessage, CancellationToken.None);
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error processing SQS record {record.MessageId}: {ex}");
                failures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = record.MessageId });
            }
        }

        return new SQSBatchResponse { BatchItemFailures = failures };
    }

    private static string ExtractPayload(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("Message", out var message))
                return message.GetString() ?? body;
        }
        catch
        {
        }

        return body;
    }

    private static string ResolveEventType(SQSEvent.SQSMessage record)
    {
        // SNS message attributes come inside the body (SNS→SQS subscription)
        try
        {
            using var doc = JsonDocument.Parse(record.Body);
            if (doc.RootElement.TryGetProperty("MessageAttributes", out var attrs)
                && attrs.TryGetProperty("eventType", out var et)
                && et.TryGetProperty("Value", out var val)
                && !string.IsNullOrWhiteSpace(val.GetString()))
            {
                return val.GetString()!;
            }
        }
        catch
        {
        }

        // Direct SQS message attributes
        if (record.MessageAttributes?.TryGetValue("eventType", out var directAttr) == true
            && !string.IsNullOrWhiteSpace(directAttr?.StringValue))
        {
            return directAttr.StringValue;
        }

        return "Unknown";
    }
}
