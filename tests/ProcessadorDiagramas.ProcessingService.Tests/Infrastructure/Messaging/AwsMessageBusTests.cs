using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

namespace ProcessadorDiagramas.ProcessingService.Tests.Infrastructure.Messaging;

public sealed class AwsMessageBusTests
{
    [Fact]
    public async Task SubscribeAsync_WhenMessageHasNoAttributes_DoesNotThrowAndUsesUnknownEventType()
    {
        var capturedEventTypes = new List<string>();
        using var cts = new CancellationTokenSource();

        var sqs = new Mock<IAmazonSQS>();
        sqs.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse
            {
                Messages =
                [
                    new Message
                    {
                        MessageId = Guid.NewGuid().ToString("N"),
                        ReceiptHandle = "receipt-handle",
                        Body = "{\"payload\":true}",
                        MessageAttributes = null!
                    }
                ]
            });
        sqs.Setup(x => x.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(new DeleteMessageResponse());

        var bus = new AwsMessageBus(
            Mock.Of<IAmazonSimpleNotificationService>(),
            sqs.Object,
            Options.Create(new AwsSettings
            {
                Region = "us-east-1",
                QueueUrl = "http://localhost:4566/000000000000/analysis-process-requests",
                TopicArn = "arn:aws:sns:us-east-1:000000000000:analysis-processing-events",
                ServiceURL = "http://localhost:4566"
            }),
            NullLogger<AwsMessageBus>.Instance);

        var act = async () => await bus.SubscribeAsync((message, _) =>
        {
            capturedEventTypes.Add(message.EventType);
            return Task.CompletedTask;
        }, cts.Token);

        await act.Should().NotThrowAsync();
        capturedEventTypes.Should().ContainSingle().Which.Should().Be("Unknown");
    }
}
