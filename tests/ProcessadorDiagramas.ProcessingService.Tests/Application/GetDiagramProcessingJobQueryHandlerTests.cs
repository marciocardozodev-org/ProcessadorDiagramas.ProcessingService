using FluentAssertions;
using Moq;
using ProcessadorDiagramas.ProcessingService.Application.Queries.GetDiagramProcessingJob;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Tests.Application;

public sealed class GetDiagramProcessingJobQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenJobExists_ReturnsMappedResponse()
    {
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "uploads/diagram.png", "corr-123");
        job.MarkAsStarted();
        job.SetPreprocessedContent("normalized-content");

        var repository = new Mock<IDiagramProcessingJobRepository>();
        repository
            .Setup(x => x.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var handler = new GetDiagramProcessingJobQueryHandler(repository.Object);

        var response = await handler.HandleAsync(new GetDiagramProcessingJobQuery(job.Id));

        response.Should().NotBeNull();
        response!.Id.Should().Be(job.Id);
        response.Status.Should().Be("InProgress");
        response.PreprocessedContent.Should().Be("normalized-content");
        response.CorrelationId.Should().Be("corr-123");
    }

    [Fact]
    public async Task HandleAsync_WhenJobDoesNotExist_ReturnsNull()
    {
        var repository = new Mock<IDiagramProcessingJobRepository>();
        repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiagramProcessingJob?)null);

        var handler = new GetDiagramProcessingJobQueryHandler(repository.Object);

        var response = await handler.HandleAsync(new GetDiagramProcessingJobQuery(Guid.NewGuid()));

        response.Should().BeNull();
    }
}