using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.ProcessingService.Application.Commands.CreateDiagramProcessingJob;
using ProcessadorDiagramas.ProcessingService.Application.Commands.ProcessDiagramProcessingJob;
using ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;

namespace ProcessadorDiagramas.ProcessingService.Application.EventHandlers;

public sealed class AnalysisProcessRequestedEventHandler : IEventHandler
{
    private readonly CreateDiagramProcessingJobCommandHandler _commandHandler;
    private readonly ProcessDiagramProcessingJobCommandHandler _processingJobCommandHandler;
    private readonly ILogger<AnalysisProcessRequestedEventHandler> _logger;

    public AnalysisProcessRequestedEventHandler(
        CreateDiagramProcessingJobCommandHandler commandHandler,
        ProcessDiagramProcessingJobCommandHandler processingJobCommandHandler,
        ILogger<AnalysisProcessRequestedEventHandler> logger)
    {
        _commandHandler = commandHandler;
        _processingJobCommandHandler = processingJobCommandHandler;
        _logger = logger;
    }

    public string EventType => nameof(AnalysisProcessRequestedEvent);

    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        AnalysisProcessRequestedEvent? @event;
        try
        {
            @event = JsonSerializer.Deserialize<AnalysisProcessRequestedEvent>(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not deserialize AnalysisProcessRequestedEvent payload.");
            return;
        }

        if (@event is null)
        {
            _logger.LogWarning("Could not deserialize AnalysisProcessRequestedEvent payload.");
            return;
        }

        try
        {
            var response = await _commandHandler.HandleAsync(
                new CreateDiagramProcessingJobCommand(
                    @event.DiagramAnalysisProcessId,
                    @event.InputStorageKey,
                    @event.CorrelationId),
                cancellationToken);

            await _processingJobCommandHandler.HandleAsync(
                new ProcessDiagramProcessingJobCommand(response.Id),
                cancellationToken);

            _logger.LogInformation(
                "Registered and processed job {JobId} for analysis process {DiagramAnalysisProcessId}.",
                response.Id,
                response.DiagramAnalysisProcessId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(
                ex,
                "Processing job for analysis process {DiagramAnalysisProcessId} is already registered. Skipping duplicate message.",
                @event.DiagramAnalysisProcessId);
        }
    }
}