using ProcessadorDiagramas.ProcessingService.Domain.Enums;

namespace ProcessadorDiagramas.ProcessingService.Domain.Entities;

public sealed class DiagramProcessingJob
{
    public Guid Id { get; private set; }
    public Guid DiagramAnalysisProcessId { get; private set; }
    public string InputStorageKey { get; private set; } = string.Empty;
    public string? PreprocessedContent { get; private set; }
    public DiagramProcessingJobStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private DiagramProcessingJob()
    {
    }

    public static DiagramProcessingJob Create(Guid diagramAnalysisProcessId, string inputStorageKey, string correlationId)
    {
        if (diagramAnalysisProcessId == Guid.Empty)
            throw new ArgumentException("Diagram analysis process id cannot be empty.", nameof(diagramAnalysisProcessId));

        if (string.IsNullOrWhiteSpace(inputStorageKey))
            throw new ArgumentException("Input storage key cannot be empty.", nameof(inputStorageKey));

        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("Correlation id cannot be empty.", nameof(correlationId));

        return new DiagramProcessingJob
        {
            Id = Guid.NewGuid(),
            DiagramAnalysisProcessId = diagramAnalysisProcessId,
            InputStorageKey = inputStorageKey.Trim(),
            CorrelationId = correlationId.Trim(),
            Status = DiagramProcessingJobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsStarted()
    {
        if (Status != DiagramProcessingJobStatus.Pending)
            throw new InvalidOperationException($"Cannot transition to InProgress from status {Status}.");

        Status = DiagramProcessingJobStatus.InProgress;
        StartedAt ??= DateTime.UtcNow;
        FailureReason = null;
        CompletedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPreprocessedContent(string preprocessedContent)
    {
        if (Status != DiagramProcessingJobStatus.InProgress)
            throw new InvalidOperationException($"Cannot assign preprocessed content while status is {Status}.");

        if (string.IsNullOrWhiteSpace(preprocessedContent))
            throw new ArgumentException("Preprocessed content cannot be empty.", nameof(preprocessedContent));

        PreprocessedContent = preprocessedContent.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsCompleted()
    {
        if (Status != DiagramProcessingJobStatus.InProgress)
            throw new InvalidOperationException($"Cannot transition to Completed from status {Status}.");

        Status = DiagramProcessingJobStatus.Completed;
        FailureReason = null;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("Failure reason cannot be empty.", nameof(failureReason));

        if (Status == DiagramProcessingJobStatus.Completed)
            throw new InvalidOperationException("Cannot transition to Failed from status Completed.");

        Status = DiagramProcessingJobStatus.Failed;
        FailureReason = failureReason.Trim();
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}