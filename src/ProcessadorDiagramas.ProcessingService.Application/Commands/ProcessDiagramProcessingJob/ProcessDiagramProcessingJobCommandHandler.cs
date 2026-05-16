using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;
using ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ProcessingService.Application.Commands.ProcessDiagramProcessingJob;

public sealed class ProcessDiagramProcessingJobCommandHandler
{
    private readonly IDiagramProcessingJobRepository _jobRepository;
    private readonly IDiagramProcessingResultRepository _resultRepository;
    private readonly IDiagramProcessingAttemptRepository _attemptRepository;
    private readonly IDiagramSourceStorage _diagramSourceStorage;
    private readonly IDiagramPreprocessor _diagramPreprocessor;
    private readonly IDiagramAiPipeline _diagramAiPipeline;
    private readonly IMessageBus _messageBus;
    private readonly IEventPublishingOptions _eventPublishingOptions;
    private readonly IAnalysisArtifactStorage _analysisArtifactStorage;
    private readonly IOutboxMessageRepository _outboxMessageRepository;
    private readonly IOutboxPublisher _outboxPublisher;
    private readonly ILogger<ProcessDiagramProcessingJobCommandHandler> _logger;

    public ProcessDiagramProcessingJobCommandHandler(
        IDiagramProcessingJobRepository jobRepository,
        IDiagramProcessingResultRepository resultRepository,
        IDiagramProcessingAttemptRepository attemptRepository,
        IDiagramSourceStorage diagramSourceStorage,
        IDiagramPreprocessor diagramPreprocessor,
        IDiagramAiPipeline diagramAiPipeline,
        IMessageBus messageBus,
        IEventPublishingOptions eventPublishingOptions,
        ILogger<ProcessDiagramProcessingJobCommandHandler> logger)
        : this(
            jobRepository,
            resultRepository,
            attemptRepository,
            diagramSourceStorage,
            diagramPreprocessor,
            diagramAiPipeline,
            messageBus,
            eventPublishingOptions,
            logger,
            null,
            null,
            null)
    {
    }

    public ProcessDiagramProcessingJobCommandHandler(
        IDiagramProcessingJobRepository jobRepository,
        IDiagramProcessingResultRepository resultRepository,
        IDiagramProcessingAttemptRepository attemptRepository,
        IDiagramSourceStorage diagramSourceStorage,
        IDiagramPreprocessor diagramPreprocessor,
        IDiagramAiPipeline diagramAiPipeline,
        IMessageBus messageBus,
        IEventPublishingOptions eventPublishingOptions,
        ILogger<ProcessDiagramProcessingJobCommandHandler> logger,
        IAnalysisArtifactStorage? analysisArtifactStorage,
        IOutboxMessageRepository? outboxMessageRepository,
        IOutboxPublisher? outboxPublisher)
    {
        _jobRepository = jobRepository;
        _resultRepository = resultRepository;
        _attemptRepository = attemptRepository;
        _diagramSourceStorage = diagramSourceStorage;
        _diagramPreprocessor = diagramPreprocessor;
        _diagramAiPipeline = diagramAiPipeline;
        _messageBus = messageBus;
        _eventPublishingOptions = eventPublishingOptions;
        _analysisArtifactStorage = analysisArtifactStorage ?? new NoOpAnalysisArtifactStorage();
        _outboxMessageRepository = outboxMessageRepository ?? new NoOpOutboxMessageRepository();
        _outboxPublisher = outboxPublisher ?? new NoOpOutboxPublisher();
        _logger = logger;
    }

    public async Task<ProcessDiagramProcessingJobResponse> HandleAsync(
        ProcessDiagramProcessingJobCommand command,
        CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(command.JobId, cancellationToken);
        if (job is null)
            throw new InvalidOperationException($"Diagram processing job {command.JobId} was not found.");

        var existingResult = await _resultRepository.GetByJobIdAsync(job.Id, cancellationToken);
        if (existingResult is not null)
        {
            return new ProcessDiagramProcessingJobResponse(
                job.Id,
                job.Status.ToString(),
                existingResult.Id,
                0,
                job.CompletedAt ?? existingResult.CreatedAt);
        }

        var attempts = await _attemptRepository.ListByJobIdAsync(job.Id, cancellationToken);
        var attempt = DiagramProcessingAttempt.Start(job.Id, attempts.Count + 1);
        await _attemptRepository.AddAsync(attempt, cancellationToken);

        try
        {
            _logger.LogInformation(
                "Starting processing for job {JobId}, analysis {DiagramAnalysisProcessId}, attempt {AttemptNumber}.",
                job.Id,
                job.DiagramAnalysisProcessId,
                attempt.AttemptNumber);

            job.MarkAsStarted();
            await _jobRepository.UpdateAsync(job, cancellationToken);
            await PublishStartedAsync(job, attempt.AttemptNumber, cancellationToken);

            _logger.LogInformation("Reading source diagram for job {JobId} from {InputStorageKey}.", job.Id, job.InputStorageKey);
            var diagramSource = await _diagramSourceStorage.ReadAsync(job.InputStorageKey, cancellationToken);

            _logger.LogInformation("Preprocessing diagram content for job {JobId}.", job.Id);
            var preprocessedContent = await _diagramPreprocessor.PreprocessAsync(diagramSource, cancellationToken);

            job.SetPreprocessedContent(preprocessedContent);
            await _jobRepository.UpdateAsync(job, cancellationToken);

            _logger.LogInformation("Submitting preprocessed diagram to AI provider for job {JobId}.", job.Id);
            var aiResult = await _diagramAiPipeline.AnalyzeAsync(preprocessedContent, cancellationToken);
            var persistedResult = DiagramProcessingResult.Create(job.Id, aiResult.RawOutput);
            await _resultRepository.AddAsync(persistedResult, cancellationToken);

            var artifact = await _analysisArtifactStorage.SaveAsync(
                job.DiagramAnalysisProcessId,
                job.Id,
                job.RequestId,
                attempt.AttemptNumber,
                aiResult.RawOutput,
                cancellationToken);

            attempt.MarkAsCompleted();
            await _attemptRepository.UpdateAsync(attempt, cancellationToken);

            job.MarkAsCompleted();
            await _jobRepository.UpdateAsync(job, cancellationToken);

            await PublishCompletedAsync(job, persistedResult, attempt.AttemptNumber, cancellationToken);
            await PublishCompletedV2Async(job, persistedResult, attempt.AttemptNumber, cancellationToken);
            await PublishAnalysisCompletedFromOutboxAsync(
                job,
                artifact,
                _diagramAiPipeline.GetType().Name.Contains("Dummy", StringComparison.OrdinalIgnoreCase) ? "mock-mode" : "completed",
                cancellationToken);

            _logger.LogInformation(
                "Completed processing for job {JobId}, analysis {DiagramAnalysisProcessId}, attempt {AttemptNumber}.",
                job.Id,
                job.DiagramAnalysisProcessId,
                attempt.AttemptNumber);

            return new ProcessDiagramProcessingJobResponse(
                job.Id,
                job.Status.ToString(),
                persistedResult.Id,
                attempt.AttemptNumber,
                job.CompletedAt ?? persistedResult.CreatedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Processing failed for job {JobId}, analysis {DiagramAnalysisProcessId}, attempt {AttemptNumber}.",
                job.Id,
                job.DiagramAnalysisProcessId,
                attempt.AttemptNumber);

            if (job.Status == Domain.Enums.DiagramProcessingJobStatus.Completed)
            {
                _logger.LogError(
                    ex,
                    "Job {JobId} is already completed and cannot transition to failed. Preserving status and rethrowing exception.",
                    job.Id);
                throw;
            }

            attempt.MarkAsFailed(ex.Message);
            await _attemptRepository.UpdateAsync(attempt, cancellationToken);

            job.MarkAsFailed(ex.Message);
            await _jobRepository.UpdateAsync(job, cancellationToken);

            await PublishFailedAsync(job, attempt.AttemptNumber, ex.Message, cancellationToken);
            await PublishFailedV2Async(job, attempt.AttemptNumber, ex.Message, cancellationToken);

            throw;
        }
    }

    private async Task PublishAnalysisCompletedFromOutboxAsync(
        DiagramProcessingJob job,
        StoredAnalysisArtifact artifact,
        string status,
        CancellationToken cancellationToken)
    {
        var @event = new AnalysisCompletedEvent(
            RequestId: job.RequestId,
            CorrelationId: job.CorrelationId,
            S3ArtifactBucket: artifact.Bucket,
            S3ArtifactKey: artifact.Key,
            Status: status);

        var payload = JsonSerializer.Serialize(@event);
        var outboxMessage = OutboxMessage.Create(
            nameof(AnalysisCompletedEvent),
            payload,
            job.CorrelationId,
            job.RequestId);

        await _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);

        _logger.LogInformation(
            "Outbox message persisted for AnalysisCompleted. requestId={RequestId} correlationId={CorrelationId} artifactBucket={ArtifactBucket} artifactKey={ArtifactKey} outboxId={OutboxId}",
            job.RequestId,
            job.CorrelationId,
            artifact.Bucket,
            artifact.Key,
            outboxMessage.Id);

        await _outboxPublisher.PublishPendingAsync(cancellationToken);
    }

    private Task PublishStartedAsync(DiagramProcessingJob job, int attemptNumber, CancellationToken cancellationToken)
    {
        var @event = new AnalysisProcessingStartedEvent(
            job.Id,
            job.DiagramAnalysisProcessId,
            job.CorrelationId,
            attemptNumber,
            job.StartedAt ?? DateTime.UtcNow);

        return _messageBus.PublishAsync(
            nameof(AnalysisProcessingStartedEvent),
            JsonSerializer.Serialize(@event),
            cancellationToken);
    }

    private Task PublishCompletedAsync(
        DiagramProcessingJob job,
        DiagramProcessingResult result,
        int attemptNumber,
        CancellationToken cancellationToken)
    {
        var @event = new AnalysisProcessingCompletedEvent(
            job.Id,
            job.DiagramAnalysisProcessId,
            job.CorrelationId,
            result.Id,
            attemptNumber,
            job.CompletedAt ?? result.CreatedAt);

        return _messageBus.PublishAsync(
            nameof(AnalysisProcessingCompletedEvent),
            JsonSerializer.Serialize(@event),
            cancellationToken);
    }

    private async Task PublishCompletedV2Async(
        DiagramProcessingJob job,
        DiagramProcessingResult result,
        int attemptNumber,
        CancellationToken cancellationToken)
    {
        if (!_eventPublishingOptions.PublishCompletedV2Enabled)
            return;

        var occurredAt = job.CompletedAt ?? result.CreatedAt;
        var messageId = Guid.NewGuid().ToString("N");
        var outputHash = BuildSha256Hash(result.RawAiOutput);

        var @event = new AnalysisProcessingCompletedV2Event(
            EventVersion: "2.0.0",
            EventType: nameof(AnalysisProcessingCompletedV2Event),
            OccurredAtUtc: occurredAt,
            CorrelationId: job.CorrelationId,
            DiagramAnalysisProcessId: job.DiagramAnalysisProcessId,
            DiagramProcessingJobId: job.Id,
            ResultId: result.Id,
            AttemptNumber: attemptNumber,
            ProcessingStatus: "Completed",
            RawAiOutput: result.RawAiOutput,
            OutputHash: outputHash,
            Trace: new EventTraceMetadata(
                ProducerService: _eventPublishingOptions.ProducerService,
                ProducerVersion: _eventPublishingOptions.ProducerVersion,
                MessageId: messageId));

        ValidateCompletedV2Event(@event);

        string payload;
        try
        {
            payload = JsonSerializer.Serialize(@event);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to serialize completed v2 event. correlationId={CorrelationId} analysisProcessId={DiagramAnalysisProcessId} jobId={JobId} resultId={ResultId} eventType={EventType} eventVersion={EventVersion}",
                job.CorrelationId,
                job.DiagramAnalysisProcessId,
                job.Id,
                result.Id,
                @event.EventType,
                @event.EventVersion);
            throw;
        }

        try
        {
            await _messageBus.PublishAsync(@event.EventType, payload, cancellationToken);
            _logger.LogInformation(
                "Published completed v2 event. correlationId={CorrelationId} analysisProcessId={DiagramAnalysisProcessId} jobId={JobId} resultId={ResultId} eventType={EventType} eventVersion={EventVersion} messageId={MessageId}",
                job.CorrelationId,
                job.DiagramAnalysisProcessId,
                job.Id,
                result.Id,
                @event.EventType,
                @event.EventVersion,
                messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish completed v2 event. correlationId={CorrelationId} analysisProcessId={DiagramAnalysisProcessId} jobId={JobId} resultId={ResultId} eventType={EventType} eventVersion={EventVersion}",
                job.CorrelationId,
                job.DiagramAnalysisProcessId,
                job.Id,
                result.Id,
                @event.EventType,
                @event.EventVersion);
            throw;
        }
    }

    private Task PublishFailedAsync(
        DiagramProcessingJob job,
        int attemptNumber,
        string failureReason,
        CancellationToken cancellationToken)
    {
        var @event = new AnalysisProcessingFailedEvent(
            job.Id,
            job.DiagramAnalysisProcessId,
            job.CorrelationId,
            attemptNumber,
            failureReason,
            job.CompletedAt ?? DateTime.UtcNow);

        return _messageBus.PublishAsync(
            nameof(AnalysisProcessingFailedEvent),
            JsonSerializer.Serialize(@event),
            cancellationToken);
    }

    private async Task PublishFailedV2Async(
        DiagramProcessingJob job,
        int attemptNumber,
        string failureReason,
        CancellationToken cancellationToken)
    {
        if (!_eventPublishingOptions.PublishFailedV2Enabled)
            return;

        var messageId = Guid.NewGuid().ToString("N");
        var @event = new AnalysisProcessingFailedV2Event(
            EventVersion: "2.0.0",
            EventType: nameof(AnalysisProcessingFailedV2Event),
            OccurredAtUtc: job.CompletedAt ?? DateTime.UtcNow,
            CorrelationId: job.CorrelationId,
            DiagramAnalysisProcessId: job.DiagramAnalysisProcessId,
            DiagramProcessingJobId: job.Id,
            AttemptNumber: attemptNumber,
            FailureReason: failureReason,
            FailureCode: null,
            Trace: new EventTraceMetadata(
                ProducerService: _eventPublishingOptions.ProducerService,
                ProducerVersion: _eventPublishingOptions.ProducerVersion,
                MessageId: messageId));

        string payload;
        try
        {
            payload = JsonSerializer.Serialize(@event);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to serialize failed v2 event. correlationId={CorrelationId} analysisProcessId={DiagramAnalysisProcessId} jobId={JobId} eventType={EventType} eventVersion={EventVersion}",
                job.CorrelationId,
                job.DiagramAnalysisProcessId,
                job.Id,
                @event.EventType,
                @event.EventVersion);
            throw;
        }

        await _messageBus.PublishAsync(@event.EventType, payload, cancellationToken);
        _logger.LogInformation(
            "Published failed v2 event. correlationId={CorrelationId} analysisProcessId={DiagramAnalysisProcessId} jobId={JobId} eventType={EventType} eventVersion={EventVersion} messageId={MessageId}",
            job.CorrelationId,
            job.DiagramAnalysisProcessId,
            job.Id,
            @event.EventType,
            @event.EventVersion,
            messageId);
    }

    private static string BuildSha256Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static void ValidateCompletedV2Event(AnalysisProcessingCompletedV2Event @event)
    {
        if (string.IsNullOrWhiteSpace(@event.CorrelationId)
            || string.IsNullOrWhiteSpace(@event.RawAiOutput)
            || string.IsNullOrWhiteSpace(@event.EventType)
            || string.IsNullOrWhiteSpace(@event.EventVersion))
        {
            throw new InvalidOperationException("Completed V2 event contains required fields with empty values.");
        }
    }

    private sealed class NoOpAnalysisArtifactStorage : IAnalysisArtifactStorage
    {
        public Task<StoredAnalysisArtifact> SaveAsync(Guid diagramAnalysisProcessId, Guid diagramProcessingJobId, string requestId, int attemptNumber, string rawAiOutput, CancellationToken cancellationToken = default)
            => Task.FromResult(new StoredAnalysisArtifact("noop", $"noop/{diagramAnalysisProcessId:N}/{diagramProcessingJobId:N}/{attemptNumber}"));
    }

    private sealed class NoOpOutboxMessageRepository : IOutboxMessageRepository
    {
        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyCollection<OutboxMessage>> ListPendingAsync(int maxCount, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<OutboxMessage>)Array.Empty<OutboxMessage>());

        public Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpOutboxPublisher : IOutboxPublisher
    {
        public Task PublishPendingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}