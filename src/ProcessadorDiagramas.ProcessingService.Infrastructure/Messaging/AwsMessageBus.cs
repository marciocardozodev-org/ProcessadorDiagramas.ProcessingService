using System.Text.Json;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;
using SnsMessageAttributeValue = Amazon.SimpleNotificationService.Model.MessageAttributeValue;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

public sealed class AwsMessageBus : IMessageBus
{
    private static readonly Meter Meter = new("ProcessadorDiagramas.ProcessingService.Messaging", "1.0.0");
    private static readonly Counter<long> MessagesReceivedCounter = Meter.CreateCounter<long>("processing_messages_received");
    private static readonly Counter<long> MessagesProcessedCounter = Meter.CreateCounter<long>("processing_messages_processed");
    private static readonly Counter<long> MessagesFailedCounter = Meter.CreateCounter<long>("processing_messages_failed");
    private static readonly Histogram<double> ProcessingLatencyHistogram = Meter.CreateHistogram<double>("processing_message_latency_ms");

    private readonly IAmazonSimpleNotificationService _sns;
    private readonly IAmazonSQS _sqs;
    private readonly AwsSettings _settings;
    private readonly ILogger<AwsMessageBus> _logger;

    public AwsMessageBus(
        IAmazonSimpleNotificationService sns,
        IAmazonSQS sqs,
        IOptions<AwsSettings> settings,
        ILogger<AwsMessageBus> logger)
    {
        _sns = sns;
        _sqs = sqs;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task PublishAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.TopicArn))
        {
            var configurationException = new InvalidOperationException("Aws:TopicArn is required for SNS publishing.");
            _logger.LogError(
                configurationException,
                "Failed to publish event due to configuration error. errorType={ErrorType} eventType={EventType}",
                "configuration",
                eventType);
            throw configurationException;
        }

        var request = new PublishRequest
        {
            TopicArn = _settings.TopicArn,
            Message = payload,
            MessageAttributes = new Dictionary<string, SnsMessageAttributeValue>
            {
                ["eventType"] = new SnsMessageAttributeValue
                {
                    DataType = "String",
                    StringValue = eventType
                }
            }
        };

        try
        {
            var response = await ExecuteWithRetryAsync(
                "PublishToSns",
                () => _sns.PublishAsync(request, cancellationToken),
                _settings.OperationRetryMaxAttempts,
                cancellationToken);

            _logger.LogInformation(
                "Published event {EventType} to topic {TopicArn}. messageId={MessageId}",
                eventType,
                _settings.TopicArn,
                response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish event to SNS. errorType={ErrorType} eventType={EventType} topicArn={TopicArn}",
                ClassifyPublishError(ex),
                eventType,
                _settings.TopicArn);
            throw;
        }
    }

    public async Task SubscribeAsync(Func<BusMessage, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var receiveRequest = new ReceiveMessageRequest
            {
                QueueUrl = _settings.QueueUrl,
                MaxNumberOfMessages = Math.Clamp(_settings.MaxNumberOfMessages, 1, 10),
                WaitTimeSeconds = Math.Clamp(_settings.WaitTimeSeconds, 0, 20),
                MessageAttributeNames = ["All"]
            };

            ReceiveMessageResponse response;
            try
            {
                response = await ExecuteWithRetryAsync(
                    "ReceiveMessages",
                    () => _sqs.ReceiveMessageAsync(receiveRequest, cancellationToken),
                    _settings.ReceiveRetryMaxAttempts,
                    cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error polling queue {QueueUrl}.", _settings.QueueUrl);
                await Task.Delay(GetBackoffDelay(1), cancellationToken);
                continue;
            }

            if (response.Messages is null || response.Messages.Count == 0)
                continue;

            foreach (var sqsMessage in response.Messages ?? [])
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var payload = ExtractPayload(sqsMessage.Body);
                    var eventType = ResolveEventType(sqsMessage.Body, sqsMessage.MessageAttributes);
                    var busMessage = new BusMessage(sqsMessage.MessageId, eventType, payload);

                    MessagesReceivedCounter.Add(1, new KeyValuePair<string, object?>("event_type", eventType));

                    await handler(busMessage, cancellationToken);

                    await ExecuteWithRetryAsync(
                        "DeleteMessage",
                        () => _sqs.DeleteMessageAsync(_settings.QueueUrl, sqsMessage.ReceiptHandle, cancellationToken),
                        _settings.OperationRetryMaxAttempts,
                        cancellationToken);

                    stopwatch.Stop();
                    MessagesProcessedCounter.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
                    ProcessingLatencyHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("event_type", eventType));
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    MessagesFailedCounter.Add(1, new KeyValuePair<string, object?>("event_type", ResolveEventType(sqsMessage.Body, sqsMessage.MessageAttributes)));
                    _logger.LogError(ex, "Error processing SQS message {MessageId}.", sqsMessage.MessageId);
                }
            }
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        string operationName,
        Func<Task<T>> operation,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, maxAttempts);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < attempts && !cancellationToken.IsCancellationRequested && IsTransient(ex))
            {
                var delay = GetBackoffDelay(attempt);
                _logger.LogWarning(
                    ex,
                    "Transient error in {OperationName}. Retrying attempt {Attempt}/{MaxAttempts} after {DelayMs}ms.",
                    operationName,
                    attempt,
                    attempts,
                    delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }

        return await operation();
    }

    private TimeSpan GetBackoffDelay(int attempt)
    {
        var baseDelay = Math.Max(50, _settings.RetryBaseDelayMilliseconds);
        var maxDelay = TimeSpan.FromSeconds(Math.Max(1, _settings.RetryMaxDelaySeconds));
        var computedMs = baseDelay * Math.Pow(2, Math.Max(0, attempt - 1));
        var delay = TimeSpan.FromMilliseconds(computedMs);

        return delay <= maxDelay ? delay : maxDelay;
    }

    private static bool IsTransient(Exception exception)
    {
        if (exception is OperationCanceledException)
            return false;

        if (exception is HttpRequestException or TaskCanceledException)
            return true;

        if (exception is AmazonServiceException amazonException)
        {
            if ((int)amazonException.StatusCode >= 500)
                return true;

            return amazonException.ErrorCode is "Throttling" or "ThrottlingException" or "RequestTimeout" or "TooManyRequestsException";
        }

        return false;
    }

    private static string ClassifyPublishError(Exception exception)
    {
        if (exception is InvalidOperationException)
            return "configuration";

        if (exception is AmazonServiceException serviceException)
        {
            if (string.Equals(serviceException.ErrorCode, "AccessDenied", StringComparison.OrdinalIgnoreCase)
                || string.Equals(serviceException.ErrorCode, "AuthorizationError", StringComparison.OrdinalIgnoreCase)
                || serviceException.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return "iam_permission";
            }

            return IsTransient(serviceException) ? "messaging_transient" : "messaging_non_transient";
        }

        return IsTransient(exception) ? "messaging_transient" : "processing_or_serialization";
    }

    private static string ResolveEventType(
        string sqsBody,
        Dictionary<string, Amazon.SQS.Model.MessageAttributeValue>? attributes)
    {
        if (attributes?.TryGetValue("eventType", out var attr) == true && !string.IsNullOrWhiteSpace(attr.StringValue))
            return attr.StringValue;

        try
        {
            using var document = JsonDocument.Parse(sqsBody);
            if (document.RootElement.TryGetProperty("MessageAttributes", out var messageAttributes)
                && messageAttributes.TryGetProperty("eventType", out var eventTypeNode)
                && eventTypeNode.TryGetProperty("Value", out var eventTypeValue)
                && !string.IsNullOrWhiteSpace(eventTypeValue.GetString()))
            {
                return eventTypeValue.GetString()!;
            }
        }
        catch
        {
        }

        return "Unknown";
    }

    private static string ExtractPayload(string sqsBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(sqsBody);
            if (doc.RootElement.TryGetProperty("Message", out var message))
                return message.GetString() ?? sqsBody;
        }
        catch
        {
        }

        return sqsBody;
    }
}