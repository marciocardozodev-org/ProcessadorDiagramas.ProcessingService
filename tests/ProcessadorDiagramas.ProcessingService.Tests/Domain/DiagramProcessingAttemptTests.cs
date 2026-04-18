using FluentAssertions;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Domain.Enums;

namespace ProcessadorDiagramas.ProcessingService.Tests.Domain;

public sealed class DiagramProcessingAttemptTests
{
    [Fact]
    public void Start_ValidInput_ReturnsStartedAttempt()
    {
        var attempt = DiagramProcessingAttempt.Start(Guid.NewGuid(), 1);

        attempt.Status.Should().Be(DiagramProcessingAttemptStatus.Started);
        attempt.AttemptNumber.Should().Be(1);
        attempt.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsCompleted_FromStarted_SetsCompletedStatus()
    {
        var attempt = DiagramProcessingAttempt.Start(Guid.NewGuid(), 1);

        attempt.MarkAsCompleted();

        attempt.Status.Should().Be(DiagramProcessingAttemptStatus.Completed);
        attempt.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsFailed_FromStarted_SetsFailedStatus()
    {
        var attempt = DiagramProcessingAttempt.Start(Guid.NewGuid(), 1);

        attempt.MarkAsFailed("Transient queue failure");

        attempt.Status.Should().Be(DiagramProcessingAttemptStatus.Failed);
        attempt.ErrorMessage.Should().Be("Transient queue failure");
    }
}