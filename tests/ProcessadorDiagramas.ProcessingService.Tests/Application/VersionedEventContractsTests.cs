using System.Text.Json;
using FluentAssertions;
using ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;

namespace ProcessadorDiagramas.ProcessingService.Tests.Application;

public sealed class VersionedEventContractsTests
{
    [Fact]
    public void AnalysisProcessingCompletedV2Event_SerializesRequiredFieldsInCamelCase()
    {
        var @event = new AnalysisProcessingCompletedV2Event(
            EventVersion: "2.0.0",
            EventType: nameof(AnalysisProcessingCompletedV2Event),
            OccurredAtUtc: new DateTime(2026, 05, 11, 21, 10, 00, DateTimeKind.Utc),
            CorrelationId: "corr-123",
            DiagramAnalysisProcessId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
            DiagramProcessingJobId: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            ResultId: Guid.Parse("99999999-8888-7777-6666-555555555555"),
            AttemptNumber: 1,
            ProcessingStatus: "Completed",
            RawAiOutput: "{\"raw\":true}",
            OutputHash: "sha256:abc123",
            Trace: new EventTraceMetadata("ProcessadorDiagramas.ProcessingService", "1.0.0", "msg-123"));

        var json = JsonSerializer.Serialize(@event);

        json.Should().Contain("\"eventVersion\":\"2.0.0\"");
        json.Should().Contain("\"eventType\":\"AnalysisProcessingCompletedV2Event\"");
        json.Should().Contain("\"correlationId\":\"corr-123\"");
        json.Should().Contain("\"rawAiOutput\":");
        json.Should().Contain("\"trace\":{");
        json.Should().Contain("\"messageId\":\"msg-123\"");
    }

    [Fact]
    public void AnalysisProcessingFailedV2Event_SerializesRequiredFieldsInCamelCase()
    {
        var @event = new AnalysisProcessingFailedV2Event(
            EventVersion: "2.0.0",
            EventType: nameof(AnalysisProcessingFailedV2Event),
            OccurredAtUtc: new DateTime(2026, 05, 11, 21, 12, 00, DateTimeKind.Utc),
            CorrelationId: "corr-456",
            DiagramAnalysisProcessId: Guid.NewGuid(),
            DiagramProcessingJobId: Guid.NewGuid(),
            AttemptNumber: 2,
            FailureReason: "provider timeout",
            FailureCode: "AI_TIMEOUT",
            Trace: new EventTraceMetadata("ProcessadorDiagramas.ProcessingService", "1.0.0", "msg-456"));

        var json = JsonSerializer.Serialize(@event);

        json.Should().Contain("\"eventVersion\":\"2.0.0\"");
        json.Should().Contain("\"eventType\":\"AnalysisProcessingFailedV2Event\"");
        json.Should().Contain("\"failureReason\":\"provider timeout\"");
        json.Should().Contain("\"failureCode\":\"AI_TIMEOUT\"");
        json.Should().Contain("\"trace\":{");
    }
}
