using System.Text.Json;
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
    private readonly ILogger<ProcessDiagramProcessingJobCommandHandler> _logger;

    public ProcessDiagramProcessingJobCommandHandler(
        IDiagramProcessingJobRepository jobRepository,
        IDiagramProcessingResultRepository resultRepository,
        IDiagramProcessingAttemptRepository attemptRepository,
        IDiagramSourceStorage diagramSourceStorage,
        IDiagramPreprocessor diagramPreprocessor,
        IDiagramAiPipeline diagramAiPipeline,
        IMessageBus messageBus,
        ILogger<ProcessDiagramProcessingJobCommandHandler> logger)
    {
        _jobRepository = jobRepository;
        _resultRepository = resultRepository;
        _attemptRepository = attemptRepository;
        _diagramSourceStorage = diagramSourceStorage;
        _diagramPreprocessor = diagramPreprocessor;
        _diagramAiPipeline = diagramAiPipeline;
        _messageBus = messageBus;
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

            attempt.MarkAsCompleted();
            await _attemptRepository.UpdateAsync(attempt, cancellationToken);

            job.MarkAsCompleted();
            await _jobRepository.UpdateAsync(job, cancellationToken);

            await PublishCompletedAsync(job, persistedResult, attempt.AttemptNumber, cancellationToken);

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

            attempt.MarkAsFailed(ex.Message);
            await _attemptRepository.UpdateAsync(attempt, cancellationToken);

            job.MarkAsFailed(ex.Message);
            await _jobRepository.UpdateAsync(job, cancellationToken);

            await PublishFailedAsync(job, attempt.AttemptNumber, ex.Message, cancellationToken);

            throw;
        }
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
}