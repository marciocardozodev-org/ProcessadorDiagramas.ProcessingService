using ProcessadorDiagramas.ProcessingService.Domain.Enums;

namespace ProcessadorDiagramas.ProcessingService.Domain.Entities;

public sealed class DiagramProcessingAttempt
{
    public Guid Id { get; private set; }
    public Guid DiagramProcessingJobId { get; private set; }
    public int AttemptNumber { get; private set; }
    public DiagramProcessingAttemptStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private DiagramProcessingAttempt()
    {
    }

    public static DiagramProcessingAttempt Start(Guid diagramProcessingJobId, int attemptNumber)
    {
        if (diagramProcessingJobId == Guid.Empty)
            throw new ArgumentException("Diagram processing job id cannot be empty.", nameof(diagramProcessingJobId));

        if (attemptNumber <= 0)
            throw new ArgumentException("Attempt number must be greater than zero.", nameof(attemptNumber));

        var utcNow = DateTime.UtcNow;

        return new DiagramProcessingAttempt
        {
            Id = Guid.NewGuid(),
            DiagramProcessingJobId = diagramProcessingJobId,
            AttemptNumber = attemptNumber,
            Status = DiagramProcessingAttemptStatus.Started,
            StartedAt = utcNow,
            CreatedAt = utcNow
        };
    }

    public void MarkAsCompleted()
    {
        if (Status != DiagramProcessingAttemptStatus.Started)
            throw new InvalidOperationException($"Cannot complete attempt from status {Status}.");

        Status = DiagramProcessingAttemptStatus.Completed;
        ErrorMessage = null;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string errorMessage)
    {
        if (Status != DiagramProcessingAttemptStatus.Started)
            throw new InvalidOperationException($"Cannot fail attempt from status {Status}.");

        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty.", nameof(errorMessage));

        Status = DiagramProcessingAttemptStatus.Failed;
        ErrorMessage = errorMessage.Trim();
        CompletedAt = DateTime.UtcNow;
    }
}