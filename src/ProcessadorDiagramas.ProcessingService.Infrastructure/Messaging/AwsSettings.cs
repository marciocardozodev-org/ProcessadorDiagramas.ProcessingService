namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

public sealed class AwsSettings
{
    public string Region { get; set; } = string.Empty;

    public string TopicArn { get; set; } = string.Empty;

    public string QueueUrl { get; set; } = string.Empty;

    public string? ServiceURL { get; set; }

    public int MaxNumberOfMessages { get; set; } = 10;

    public int WaitTimeSeconds { get; set; } = 20;

    public int ReceiveRetryMaxAttempts { get; set; } = 5;

    public int OperationRetryMaxAttempts { get; set; } = 3;

    public int RetryBaseDelayMilliseconds { get; set; } = 250;

    public int RetryMaxDelaySeconds { get; set; } = 15;
}