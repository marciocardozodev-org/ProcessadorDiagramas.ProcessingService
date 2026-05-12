using FluentAssertions;
using Moq;
using ProcessadorDiagramas.ProcessingService.Application.Queries.GetDiagramProcessingJobByAnalysisProcessId;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Tests.Application;

public sealed class GetDiagramProcessingJobByAnalysisProcessIdQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenJobExistsWithResult_ReturnsMappedResponseWithRawAiOutput()
    {
        var analysisProcessId = Guid.NewGuid();
        var job = DiagramProcessingJob.Create(analysisProcessId, "uploads/diagram.png", "corr-123");
        job.MarkAsStarted();
        job.MarkAsCompleted();

        var result = DiagramProcessingResult.Create(job.Id, "{\"raw\":true}");

        var jobRepository = new Mock<IDiagramProcessingJobRepository>();
        jobRepository
            .Setup(x => x.GetByDiagramAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var resultRepository = new Mock<IDiagramProcessingResultRepository>();
        resultRepository
            .Setup(x => x.GetByJobIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var handler = new GetDiagramProcessingJobByAnalysisProcessIdQueryHandler(
            jobRepository.Object,
            resultRepository.Object);

        var response = await handler.HandleAsync(
            new GetDiagramProcessingJobByAnalysisProcessIdQuery(analysisProcessId));

        response.Should().NotBeNull();
        response!.Id.Should().Be(job.Id);
        response.DiagramAnalysisProcessId.Should().Be(analysisProcessId);
        response.CorrelationId.Should().Be("corr-123");
        response.Status.Should().Be("Completed");
        response.RawAiOutput.Should().Be("{\"raw\":true}");
        response.FailureReason.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenJobExistsButNotCompleted_ReturnsResponseWithoutRawAiOutput()
    {
        var analysisProcessId = Guid.NewGuid();
        var job = DiagramProcessingJob.Create(analysisProcessId, "uploads/diagram.png", "corr-456");
        job.MarkAsStarted();

        var jobRepository = new Mock<IDiagramProcessingJobRepository>();
        jobRepository
            .Setup(x => x.GetByDiagramAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var resultRepository = new Mock<IDiagramProcessingResultRepository>();
        resultRepository
            .Setup(x => x.GetByJobIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiagramProcessingResult?)null);

        var handler = new GetDiagramProcessingJobByAnalysisProcessIdQueryHandler(
            jobRepository.Object,
            resultRepository.Object);

        var response = await handler.HandleAsync(
            new GetDiagramProcessingJobByAnalysisProcessIdQuery(analysisProcessId));

        response.Should().NotBeNull();
        response!.Status.Should().Be("InProgress");
        response.RawAiOutput.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenJobExistsButFailed_ReturnsResponseWithFailureReason()
    {
        var analysisProcessId = Guid.NewGuid();
        var job = DiagramProcessingJob.Create(analysisProcessId, "uploads/diagram.png", "corr-789");
        job.MarkAsStarted();
        job.MarkAsFailed("Storage read error");

        var jobRepository = new Mock<IDiagramProcessingJobRepository>();
        jobRepository
            .Setup(x => x.GetByDiagramAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var resultRepository = new Mock<IDiagramProcessingResultRepository>();
        resultRepository
            .Setup(x => x.GetByJobIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiagramProcessingResult?)null);

        var handler = new GetDiagramProcessingJobByAnalysisProcessIdQueryHandler(
            jobRepository.Object,
            resultRepository.Object);

        var response = await handler.HandleAsync(
            new GetDiagramProcessingJobByAnalysisProcessIdQuery(analysisProcessId));

        response.Should().NotBeNull();
        response!.Status.Should().Be("Failed");
        response.FailureReason.Should().Be("Storage read error");
        response.RawAiOutput.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenJobDoesNotExist_ReturnsNull()
    {
        var analysisProcessId = Guid.NewGuid();

        var jobRepository = new Mock<IDiagramProcessingJobRepository>();
        jobRepository
            .Setup(x => x.GetByDiagramAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiagramProcessingJob?)null);

        var resultRepository = new Mock<IDiagramProcessingResultRepository>();

        var handler = new GetDiagramProcessingJobByAnalysisProcessIdQueryHandler(
            jobRepository.Object,
            resultRepository.Object);

        var response = await handler.HandleAsync(
            new GetDiagramProcessingJobByAnalysisProcessIdQuery(analysisProcessId));

        response.Should().BeNull();
    }
}
