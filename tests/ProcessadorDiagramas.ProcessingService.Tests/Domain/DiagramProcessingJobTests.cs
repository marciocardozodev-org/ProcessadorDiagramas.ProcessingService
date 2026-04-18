using FluentAssertions;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Domain.Enums;

namespace ProcessadorDiagramas.ProcessingService.Tests.Domain;

public sealed class DiagramProcessingJobTests
{
    [Fact]
    public void Create_ValidInput_ReturnsPendingJob()
    {
        var analysisProcessId = Guid.NewGuid();

        var job = DiagramProcessingJob.Create(analysisProcessId, "uploads/diagram.png", "corr-123");

        job.Id.Should().NotBeEmpty();
        job.DiagramAnalysisProcessId.Should().Be(analysisProcessId);
        job.InputStorageKey.Should().Be("uploads/diagram.png");
        job.CorrelationId.Should().Be("corr-123");
        job.Status.Should().Be(DiagramProcessingJobStatus.Pending);
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
        job.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_InvalidStorageKey_ThrowsArgumentException(string storageKey)
    {
        var act = () => DiagramProcessingJob.Create(Guid.NewGuid(), storageKey, "corr-123");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsStarted_FromPending_SetsInProgressStatus()
    {
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "uploads/diagram.png", "corr-123");

        job.MarkAsStarted();

        job.Status.Should().Be(DiagramProcessingJobStatus.InProgress);
        job.StartedAt.Should().NotBeNull();
        job.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void SetPreprocessedContent_FromInProgress_StoresContent()
    {
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "uploads/diagram.png", "corr-123");
        job.MarkAsStarted();

        job.SetPreprocessedContent("normalized-diagram-content");

        job.PreprocessedContent.Should().Be("normalized-diagram-content");
        job.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsCompleted_FromInProgress_SetsCompletedStatus()
    {
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "uploads/diagram.png", "corr-123");
        job.MarkAsStarted();

        job.MarkAsCompleted();

        job.Status.Should().Be(DiagramProcessingJobStatus.Completed);
        job.CompletedAt.Should().NotBeNull();
        job.FailureReason.Should().BeNull();
    }

    [Fact]
    public void MarkAsFailed_FromInProgress_SetsFailedStatus()
    {
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "uploads/diagram.png", "corr-123");
        job.MarkAsStarted();

        job.MarkAsFailed("AI provider timeout");

        job.Status.Should().Be(DiagramProcessingJobStatus.Failed);
        job.FailureReason.Should().Be("AI provider timeout");
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsCompleted_FromPending_ThrowsInvalidOperationException()
    {
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "uploads/diagram.png", "corr-123");

        var act = () => job.MarkAsCompleted();

        act.Should().Throw<InvalidOperationException>();
    }
}