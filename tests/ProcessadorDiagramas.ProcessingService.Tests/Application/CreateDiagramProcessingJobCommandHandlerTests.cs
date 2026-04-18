using FluentAssertions;
using Moq;
using ProcessadorDiagramas.ProcessingService.Application.Commands.CreateDiagramProcessingJob;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Tests.Application;

public sealed class CreateDiagramProcessingJobCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenJobDoesNotExist_CreatesPendingJob()
    {
        var repository = new Mock<IDiagramProcessingJobRepository>();
        repository
            .Setup(x => x.GetByDiagramAnalysisProcessIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiagramProcessingJob?)null);

        var handler = new CreateDiagramProcessingJobCommandHandler(repository.Object);
        var command = new CreateDiagramProcessingJobCommand(Guid.NewGuid(), "uploads/diagram.png", "corr-123");

        var response = await handler.HandleAsync(command);

        response.Id.Should().NotBeEmpty();
        response.DiagramAnalysisProcessId.Should().Be(command.DiagramAnalysisProcessId);
        response.Status.Should().Be("Pending");
        repository.Verify(x => x.AddAsync(It.IsAny<DiagramProcessingJob>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenJobAlreadyExists_ThrowsInvalidOperationException()
    {
        var existing = DiagramProcessingJob.Create(Guid.NewGuid(), "uploads/diagram.png", "corr-123");
        var repository = new Mock<IDiagramProcessingJobRepository>();
        repository
            .Setup(x => x.GetByDiagramAnalysisProcessIdAsync(existing.DiagramAnalysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = new CreateDiagramProcessingJobCommandHandler(repository.Object);
        var command = new CreateDiagramProcessingJobCommand(existing.DiagramAnalysisProcessId, "uploads/diagram.png", "corr-456");

        var act = () => handler.HandleAsync(command);

        await act.Should().ThrowAsync<InvalidOperationException>();
        repository.Verify(x => x.AddAsync(It.IsAny<DiagramProcessingJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}