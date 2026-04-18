namespace ProcessadorDiagramas.ProcessingService.Domain.Entities;

public sealed class DiagramProcessingResult
{
    public Guid Id { get; private set; }
    public Guid DiagramProcessingJobId { get; private set; }
    public string RawAiOutput { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private DiagramProcessingResult()
    {
    }

    public static DiagramProcessingResult Create(Guid diagramProcessingJobId, string rawAiOutput)
    {
        if (diagramProcessingJobId == Guid.Empty)
            throw new ArgumentException("Diagram processing job id cannot be empty.", nameof(diagramProcessingJobId));

        if (string.IsNullOrWhiteSpace(rawAiOutput))
            throw new ArgumentException("Raw AI output cannot be empty.", nameof(rawAiOutput));

        return new DiagramProcessingResult
        {
            Id = Guid.NewGuid(),
            DiagramProcessingJobId = diagramProcessingJobId,
            RawAiOutput = rawAiOutput.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }
}