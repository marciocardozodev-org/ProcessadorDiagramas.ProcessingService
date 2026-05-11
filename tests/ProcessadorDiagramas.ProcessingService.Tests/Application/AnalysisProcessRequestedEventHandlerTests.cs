using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProcessadorDiagramas.ProcessingService.Application.Commands.CreateDiagramProcessingJob;
using ProcessadorDiagramas.ProcessingService.Application.Commands.ProcessDiagramProcessingJob;
using ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;
using ProcessadorDiagramas.ProcessingService.Application.EventHandlers;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Tests.Application;

public sealed class AnalysisProcessRequestedEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidPayload_CreatesProcessingJob()
    {
        var repository = new Mock<IDiagramProcessingJobRepository>();
        repository
            .Setup(x => x.GetByDiagramAnalysisProcessIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiagramProcessingJob?)null);
        repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => DiagramProcessingJob.Create(Guid.NewGuid(), "uploads/diagram.png", "corr-123"));
        repository
            .Setup(x => x.UpdateAsync(It.IsAny<DiagramProcessingJob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var resultRepository = new Mock<IDiagramProcessingResultRepository>();
        resultRepository
            .Setup(x => x.GetByJobIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiagramProcessingResult?)null);
        resultRepository
            .Setup(x => x.AddAsync(It.IsAny<DiagramProcessingResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var attemptRepository = new Mock<IDiagramProcessingAttemptRepository>();
        attemptRepository
            .Setup(x => x.ListByJobIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DiagramProcessingAttempt>());
        attemptRepository
            .Setup(x => x.AddAsync(It.IsAny<DiagramProcessingAttempt>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        attemptRepository
            .Setup(x => x.UpdateAsync(It.IsAny<DiagramProcessingAttempt>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sourceStorage = new Mock<IDiagramSourceStorage>();
        sourceStorage
            .Setup(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredDiagramSource("uploads/diagram.png", "diagram.png", "image/png", [1, 2, 3]));

        var preprocessor = new Mock<IDiagramPreprocessor>();
        preprocessor
            .Setup(x => x.PreprocessAsync(It.IsAny<StoredDiagramSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("preprocessed-content");

        var aiPipeline = new Mock<IDiagramAiPipeline>();
        aiPipeline
            .Setup(x => x.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagramAiPipelineResult("{\"raw\":true}"));

        var messageBus = new Mock<IMessageBus>();
        messageBus.Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var eventPublishingOptions = new Mock<IEventPublishingOptions>();
        eventPublishingOptions.SetupGet(x => x.PublishCompletedV2Enabled).Returns(true);
        eventPublishingOptions.SetupGet(x => x.PublishFailedV2Enabled).Returns(true);
        eventPublishingOptions.SetupGet(x => x.ProducerService).Returns("ProcessadorDiagramas.ProcessingService");
        eventPublishingOptions.SetupGet(x => x.ProducerVersion).Returns("1.0.0-test");

        var commandHandler = new CreateDiagramProcessingJobCommandHandler(repository.Object);
        var processingHandler = new ProcessDiagramProcessingJobCommandHandler(
            repository.Object,
            resultRepository.Object,
            attemptRepository.Object,
            sourceStorage.Object,
            preprocessor.Object,
            aiPipeline.Object,
            messageBus.Object,
            eventPublishingOptions.Object,
            NullLogger<ProcessDiagramProcessingJobCommandHandler>.Instance);

        var handler = new AnalysisProcessRequestedEventHandler(commandHandler, processingHandler, NullLogger<AnalysisProcessRequestedEventHandler>.Instance);
        var @event = new AnalysisProcessRequestedEvent(Guid.NewGuid(), "uploads/diagram.png", "corr-123", DateTime.UtcNow);

        await handler.HandleAsync(JsonSerializer.Serialize(@event));

        repository.Verify(x => x.AddAsync(It.IsAny<DiagramProcessingJob>(), It.IsAny<CancellationToken>()), Times.Once);
        resultRepository.Verify(x => x.AddAsync(It.IsAny<DiagramProcessingResult>(), It.IsAny<CancellationToken>()), Times.Once);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingStartedEvent), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingCompletedEvent), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingCompletedV2Event), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidPayload_DoesNotThrow()
    {
        var repository = new Mock<IDiagramProcessingJobRepository>();
        var commandHandler = new CreateDiagramProcessingJobCommandHandler(repository.Object);
        var eventPublishingOptions = new Mock<IEventPublishingOptions>();
        eventPublishingOptions.SetupGet(x => x.PublishCompletedV2Enabled).Returns(true);
        eventPublishingOptions.SetupGet(x => x.PublishFailedV2Enabled).Returns(true);
        eventPublishingOptions.SetupGet(x => x.ProducerService).Returns("ProcessadorDiagramas.ProcessingService");
        eventPublishingOptions.SetupGet(x => x.ProducerVersion).Returns("1.0.0-test");
        var processingHandler = new ProcessDiagramProcessingJobCommandHandler(
            Mock.Of<IDiagramProcessingJobRepository>(),
            Mock.Of<IDiagramProcessingResultRepository>(),
            Mock.Of<IDiagramProcessingAttemptRepository>(),
            Mock.Of<IDiagramSourceStorage>(),
            Mock.Of<IDiagramPreprocessor>(),
            Mock.Of<IDiagramAiPipeline>(),
            Mock.Of<IMessageBus>(),
            eventPublishingOptions.Object,
            NullLogger<ProcessDiagramProcessingJobCommandHandler>.Instance);
        var handler = new AnalysisProcessRequestedEventHandler(commandHandler, processingHandler, NullLogger<AnalysisProcessRequestedEventHandler>.Instance);

        var act = () => handler.HandleAsync("{invalid-json");

        await act.Should().NotThrowAsync();
        repository.Verify(x => x.AddAsync(It.IsAny<DiagramProcessingJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}