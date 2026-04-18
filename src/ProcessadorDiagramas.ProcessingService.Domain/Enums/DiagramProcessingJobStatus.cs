namespace ProcessadorDiagramas.ProcessingService.Domain.Enums;

public enum DiagramProcessingJobStatus
{
    /// <summary>Message consumed and job registered, waiting for execution.</summary>
    Pending = 1,

    /// <summary>Technical processing has started.</summary>
    InProgress = 2,

    /// <summary>Processing finished successfully.</summary>
    Completed = 3,

    /// <summary>Processing ended with failure.</summary>
    Failed = 4
}