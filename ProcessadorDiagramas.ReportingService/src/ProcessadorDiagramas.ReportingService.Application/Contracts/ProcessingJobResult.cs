namespace ProcessadorDiagramas.ReportingService.Application.Contracts;

/// <summary>Resultado bruto de análise retornado pelo ProcessingService.</summary>
public sealed record ProcessingJobResult(
    Guid JobId,
    Guid DiagramAnalysisProcessId,
    string JobStatus,
    string? RawAiOutput,
    DateTime? CompletedAt);
