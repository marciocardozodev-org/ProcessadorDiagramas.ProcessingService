using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;
using ProcessadorDiagramas.ProcessingService.Application.Commands.ProcessDiagramProcessingJob;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;
using ProcessadorDiagramas.ProcessingService.Domain.Enums;

namespace ProcessadorDiagramas.ProcessingService.Tests.Application;

public sealed class ProcessDiagramProcessingJobCommandHandlerTests
{
    private static Mock<IEventPublishingOptions> CreateEventPublishingOptions(
        bool publishCompletedV2Enabled = true,
        bool publishFailedV2Enabled = true)
    {
        var options = new Mock<IEventPublishingOptions>();
        options.SetupGet(x => x.PublishCompletedV2Enabled).Returns(publishCompletedV2Enabled);
        options.SetupGet(x => x.PublishFailedV2Enabled).Returns(publishFailedV2Enabled);
        options.SetupGet(x => x.ProducerService).Returns("ProcessadorDiagramas.ProcessingService");
        options.SetupGet(x => x.ProducerVersion).Returns("1.0.0-test");
        return options;
    }

    [Fact]
    public async Task HandleAsync_WithValidJob_ProcessesAndPersistsResult()
    {
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "/tmp/uploads/diagram.png", "corr-123");
        var jobRepository = new Mock<IDiagramProcessingJobRepository>();
        jobRepository.Setup(x => x.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        jobRepository.Setup(x => x.UpdateAsync(It.IsAny<DiagramProcessingJob>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var resultRepository = new Mock<IDiagramProcessingResultRepository>();
        resultRepository.Setup(x => x.GetByJobIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync((DiagramProcessingResult?)null);
        resultRepository.Setup(x => x.AddAsync(It.IsAny<DiagramProcessingResult>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var attemptRepository = new Mock<IDiagramProcessingAttemptRepository>();
        attemptRepository.Setup(x => x.ListByJobIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<DiagramProcessingAttempt>());
        attemptRepository.Setup(x => x.AddAsync(It.IsAny<DiagramProcessingAttempt>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        attemptRepository.Setup(x => x.UpdateAsync(It.IsAny<DiagramProcessingAttempt>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var storage = new Mock<IDiagramSourceStorage>();
        storage.Setup(x => x.ReadAsync(job.InputStorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredDiagramSource(job.InputStorageKey, "diagram.png", "image/png", [1, 2, 3]));

        var preprocessor = new Mock<IDiagramPreprocessor>();
        preprocessor.Setup(x => x.PreprocessAsync(It.IsAny<StoredDiagramSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("preprocessed-content");

        var aiPipeline = new Mock<IDiagramAiPipeline>();
        aiPipeline.Setup(x => x.AnalyzeAsync("preprocessed-content", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagramAiPipelineResult("{\"raw\":true}"));

        var messageBus = new Mock<IMessageBus>();
        messageBus.Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ProcessDiagramProcessingJobCommandHandler(
            jobRepository.Object,
            resultRepository.Object,
            attemptRepository.Object,
            storage.Object,
            preprocessor.Object,
            aiPipeline.Object,
            messageBus.Object,
            CreateEventPublishingOptions().Object,
            NullLogger<ProcessDiagramProcessingJobCommandHandler>.Instance);

        var response = await handler.HandleAsync(new ProcessDiagramProcessingJobCommand(job.Id));

        response.JobId.Should().Be(job.Id);
        response.Status.Should().Be("Completed");
        response.AttemptNumber.Should().Be(1);
        job.Status.Should().Be(DiagramProcessingJobStatus.Completed);
        job.PreprocessedContent.Should().Be("preprocessed-content");
        resultRepository.Verify(x => x.AddAsync(It.IsAny<DiagramProcessingResult>(), It.IsAny<CancellationToken>()), Times.Once);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingStartedEvent), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingCompletedEvent), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingCompletedV2Event), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingFailedEvent), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingFailedV2Event), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenProcessingFails_MarksJobAndAttemptAsFailed()
    {
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "/tmp/uploads/missing.png", "corr-123");
        DiagramProcessingAttempt? updatedAttempt = null;

        var jobRepository = new Mock<IDiagramProcessingJobRepository>();
        jobRepository.Setup(x => x.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        jobRepository.Setup(x => x.UpdateAsync(It.IsAny<DiagramProcessingJob>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var resultRepository = new Mock<IDiagramProcessingResultRepository>();
        resultRepository.Setup(x => x.GetByJobIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync((DiagramProcessingResult?)null);

        var attemptRepository = new Mock<IDiagramProcessingAttemptRepository>();
        attemptRepository.Setup(x => x.ListByJobIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<DiagramProcessingAttempt>());
        attemptRepository.Setup(x => x.AddAsync(It.IsAny<DiagramProcessingAttempt>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        attemptRepository
            .Setup(x => x.UpdateAsync(It.IsAny<DiagramProcessingAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<DiagramProcessingAttempt, CancellationToken>((attempt, _) => updatedAttempt = attempt)
            .Returns(Task.CompletedTask);

        var storage = new Mock<IDiagramSourceStorage>();
        storage.Setup(x => x.ReadAsync(job.InputStorageKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("missing"));

        var messageBus = new Mock<IMessageBus>();
        messageBus.Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ProcessDiagramProcessingJobCommandHandler(
            jobRepository.Object,
            resultRepository.Object,
            attemptRepository.Object,
            storage.Object,
            Mock.Of<IDiagramPreprocessor>(),
            Mock.Of<IDiagramAiPipeline>(),
            messageBus.Object,
            CreateEventPublishingOptions().Object,
            NullLogger<ProcessDiagramProcessingJobCommandHandler>.Instance);

        var act = () => handler.HandleAsync(new ProcessDiagramProcessingJobCommand(job.Id));

        await act.Should().ThrowAsync<FileNotFoundException>();
        job.Status.Should().Be(DiagramProcessingJobStatus.Failed);
        updatedAttempt.Should().NotBeNull();
        updatedAttempt!.Status.Should().Be(DiagramProcessingAttemptStatus.Failed);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingStartedEvent), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingFailedEvent), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingFailedV2Event), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingCompletedEvent), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingCompletedV2Event), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithValidJob_PublishesStructuredCompletedPayloadForLegacyAndV2()
    {
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "/tmp/uploads/diagram.png", "corr-123");
        string? completedPayload = null;
        string? completedV2Payload = null;

        var jobRepository = new Mock<IDiagramProcessingJobRepository>();
        jobRepository.Setup(x => x.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        jobRepository.Setup(x => x.UpdateAsync(It.IsAny<DiagramProcessingJob>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var resultRepository = new Mock<IDiagramProcessingResultRepository>();
        resultRepository.Setup(x => x.GetByJobIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync((DiagramProcessingResult?)null);
        resultRepository.Setup(x => x.AddAsync(It.IsAny<DiagramProcessingResult>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var attemptRepository = new Mock<IDiagramProcessingAttemptRepository>();
        attemptRepository.Setup(x => x.ListByJobIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<DiagramProcessingAttempt>());
        attemptRepository.Setup(x => x.AddAsync(It.IsAny<DiagramProcessingAttempt>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        attemptRepository.Setup(x => x.UpdateAsync(It.IsAny<DiagramProcessingAttempt>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var storage = new Mock<IDiagramSourceStorage>();
        storage.Setup(x => x.ReadAsync(job.InputStorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredDiagramSource(job.InputStorageKey, "diagram.png", "image/png", [1, 2, 3]));

        var preprocessor = new Mock<IDiagramPreprocessor>();
        preprocessor.Setup(x => x.PreprocessAsync(It.IsAny<StoredDiagramSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("preprocessed-content");

        var aiPipeline = new Mock<IDiagramAiPipeline>();
        aiPipeline.Setup(x => x.AnalyzeAsync("preprocessed-content", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagramAiPipelineResult("{\"raw\":true}"));

        var messageBus = new Mock<IMessageBus>();
        messageBus.Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        messageBus.Setup(x => x.PublishAsync(nameof(AnalysisProcessingCompletedEvent), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, payload, _) => completedPayload = payload)
            .Returns(Task.CompletedTask);
        messageBus.Setup(x => x.PublishAsync(nameof(AnalysisProcessingCompletedV2Event), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, payload, _) => completedV2Payload = payload)
            .Returns(Task.CompletedTask);

        var handler = new ProcessDiagramProcessingJobCommandHandler(
            jobRepository.Object,
            resultRepository.Object,
            attemptRepository.Object,
            storage.Object,
            preprocessor.Object,
            aiPipeline.Object,
            messageBus.Object,
            CreateEventPublishingOptions().Object,
            NullLogger<ProcessDiagramProcessingJobCommandHandler>.Instance);

        await handler.HandleAsync(new ProcessDiagramProcessingJobCommand(job.Id));

        completedPayload.Should().NotBeNull();
        var completedEvent = JsonSerializer.Deserialize<AnalysisProcessingCompletedEvent>(completedPayload!);
        completedEvent.Should().NotBeNull();
        completedEvent!.DiagramProcessingJobId.Should().Be(job.Id);
        completedEvent.DiagramAnalysisProcessId.Should().Be(job.DiagramAnalysisProcessId);
        completedEvent.CorrelationId.Should().Be("corr-123");
        completedEvent.AttemptNumber.Should().Be(1);

        completedV2Payload.Should().NotBeNull();
        var completedV2Event = JsonSerializer.Deserialize<AnalysisProcessingCompletedV2Event>(completedV2Payload!);
        completedV2Event.Should().NotBeNull();
        completedV2Event!.EventVersion.Should().Be("2.0.0");
        completedV2Event.EventType.Should().Be(nameof(AnalysisProcessingCompletedV2Event));
        completedV2Event.DiagramProcessingJobId.Should().Be(job.Id);
        completedV2Event.DiagramAnalysisProcessId.Should().Be(job.DiagramAnalysisProcessId);
        completedV2Event.CorrelationId.Should().Be("corr-123");
        completedV2Event.ProcessingStatus.Should().Be("Completed");
        completedV2Event.RawAiOutput.Should().Be("{\"raw\":true}");
        completedV2Event.OutputHash.Should().StartWith("sha256:");
        completedV2Event.Trace.ProducerService.Should().Be("ProcessadorDiagramas.ProcessingService");
        completedV2Event.Trace.MessageId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task HandleAsync_WhenCompletedV2FlagDisabled_PublishesOnlyLegacyCompleted()
    {
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "/tmp/uploads/diagram.png", "corr-123");

        var jobRepository = new Mock<IDiagramProcessingJobRepository>();
        jobRepository.Setup(x => x.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        jobRepository.Setup(x => x.UpdateAsync(It.IsAny<DiagramProcessingJob>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var resultRepository = new Mock<IDiagramProcessingResultRepository>();
        resultRepository.Setup(x => x.GetByJobIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync((DiagramProcessingResult?)null);
        resultRepository.Setup(x => x.AddAsync(It.IsAny<DiagramProcessingResult>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var attemptRepository = new Mock<IDiagramProcessingAttemptRepository>();
        attemptRepository.Setup(x => x.ListByJobIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<DiagramProcessingAttempt>());
        attemptRepository.Setup(x => x.AddAsync(It.IsAny<DiagramProcessingAttempt>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        attemptRepository.Setup(x => x.UpdateAsync(It.IsAny<DiagramProcessingAttempt>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var storage = new Mock<IDiagramSourceStorage>();
        storage.Setup(x => x.ReadAsync(job.InputStorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredDiagramSource(job.InputStorageKey, "diagram.png", "image/png", [1, 2, 3]));

        var preprocessor = new Mock<IDiagramPreprocessor>();
        preprocessor.Setup(x => x.PreprocessAsync(It.IsAny<StoredDiagramSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("preprocessed-content");

        var aiPipeline = new Mock<IDiagramAiPipeline>();
        aiPipeline.Setup(x => x.AnalyzeAsync("preprocessed-content", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagramAiPipelineResult("{\"raw\":true}"));

        var messageBus = new Mock<IMessageBus>();
        messageBus.Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ProcessDiagramProcessingJobCommandHandler(
            jobRepository.Object,
            resultRepository.Object,
            attemptRepository.Object,
            storage.Object,
            preprocessor.Object,
            aiPipeline.Object,
            messageBus.Object,
            CreateEventPublishingOptions(publishCompletedV2Enabled: false).Object,
            NullLogger<ProcessDiagramProcessingJobCommandHandler>.Instance);

        await handler.HandleAsync(new ProcessDiagramProcessingJobCommand(job.Id));

        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingCompletedEvent), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingCompletedV2Event), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCompletedV2PublishFails_LogsAndPreservesCompletedStatus()
    {
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "/tmp/uploads/diagram.png", "corr-123");

        var jobRepository = new Mock<IDiagramProcessingJobRepository>();
        jobRepository.Setup(x => x.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        jobRepository.Setup(x => x.UpdateAsync(It.IsAny<DiagramProcessingJob>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var resultRepository = new Mock<IDiagramProcessingResultRepository>();
        resultRepository.Setup(x => x.GetByJobIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync((DiagramProcessingResult?)null);
        resultRepository.Setup(x => x.AddAsync(It.IsAny<DiagramProcessingResult>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var attemptRepository = new Mock<IDiagramProcessingAttemptRepository>();
        attemptRepository.Setup(x => x.ListByJobIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<DiagramProcessingAttempt>());
        attemptRepository.Setup(x => x.AddAsync(It.IsAny<DiagramProcessingAttempt>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        attemptRepository.Setup(x => x.UpdateAsync(It.IsAny<DiagramProcessingAttempt>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var storage = new Mock<IDiagramSourceStorage>();
        storage.Setup(x => x.ReadAsync(job.InputStorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredDiagramSource(job.InputStorageKey, "diagram.png", "image/png", [1, 2, 3]));

        var preprocessor = new Mock<IDiagramPreprocessor>();
        preprocessor.Setup(x => x.PreprocessAsync(It.IsAny<StoredDiagramSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("preprocessed-content");

        var aiPipeline = new Mock<IDiagramAiPipeline>();
        aiPipeline.Setup(x => x.AnalyzeAsync("preprocessed-content", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagramAiPipelineResult("{\"raw\":true}"));

        var messageBus = new Mock<IMessageBus>();
        messageBus.Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        messageBus.Setup(x => x.PublishAsync(nameof(AnalysisProcessingCompletedV2Event), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("publish failure"));

        var logger = new Mock<ILogger<ProcessDiagramProcessingJobCommandHandler>>();

        var handler = new ProcessDiagramProcessingJobCommandHandler(
            jobRepository.Object,
            resultRepository.Object,
            attemptRepository.Object,
            storage.Object,
            preprocessor.Object,
            aiPipeline.Object,
            messageBus.Object,
            CreateEventPublishingOptions().Object,
            logger.Object);

        var act = () => handler.HandleAsync(new ProcessDiagramProcessingJobCommand(job.Id));

        await act.Should().ThrowAsync<InvalidOperationException>();
        job.Status.Should().Be(DiagramProcessingJobStatus.Completed);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingFailedEvent), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        messageBus.Verify(x => x.PublishAsync(nameof(AnalysisProcessingFailedV2Event), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to publish completed v2 event")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}