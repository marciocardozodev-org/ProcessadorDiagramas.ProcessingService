using ProcessadorDiagramas.ReportingService.Domain.Enums;

namespace ProcessadorDiagramas.ReportingService.Domain.Entities;

/// <summary>
/// Entidade principal do domínio de relatórios.
/// Representa o relatório técnico estruturado produzido a partir dos dados brutos de análise de um diagrama.
/// </summary>
public sealed class AnalysisReport
{
    public Guid Id { get; private set; }

    /// <summary>Referência ao processo de análise no UploadOrchestration/ProcessingService.</summary>
    public Guid AnalysisProcessId { get; private set; }

    public AnalysisReportStatus Status { get; private set; }

    /// <summary>Resumo dos componentes identificados no diagrama (JSON estruturado).</summary>
    public string? ComponentsSummary { get; private set; }

    /// <summary>Riscos arquiteturais identificados (JSON estruturado).</summary>
    public string? ArchitecturalRisks { get; private set; }

    /// <summary>Recomendações técnicas (JSON estruturado).</summary>
    public string? Recommendations { get; private set; }

    /// <summary>Referência ao resultado bruto do ProcessingService que originou este relatório.</summary>
    public string? SourceAnalysisReference { get; private set; }

    /// <summary>Versão do relatório — permite regeneração controlada.</summary>
    public int Version { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTime? GeneratedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private AnalysisReport()
    {
    }

    public static AnalysisReport CreatePending(Guid analysisProcessId)
    {
        if (analysisProcessId == Guid.Empty)
            throw new ArgumentException("Analysis process id cannot be empty.", nameof(analysisProcessId));

        return new AnalysisReport
        {
            Id = Guid.NewGuid(),
            AnalysisProcessId = analysisProcessId,
            Status = AnalysisReportStatus.Pending,
            Version = 1,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsGenerated(
        string componentsSummary,
        string architecturalRisks,
        string recommendations,
        string sourceAnalysisReference)
    {
        if (string.IsNullOrWhiteSpace(componentsSummary))
            throw new ArgumentException("Components summary cannot be empty.", nameof(componentsSummary));

        if (string.IsNullOrWhiteSpace(architecturalRisks))
            throw new ArgumentException("Architectural risks cannot be empty.", nameof(architecturalRisks));

        if (string.IsNullOrWhiteSpace(recommendations))
            throw new ArgumentException("Recommendations cannot be empty.", nameof(recommendations));

        if (string.IsNullOrWhiteSpace(sourceAnalysisReference))
            throw new ArgumentException("Source analysis reference cannot be empty.", nameof(sourceAnalysisReference));

        ComponentsSummary = componentsSummary.Trim();
        ArchitecturalRisks = architecturalRisks.Trim();
        Recommendations = recommendations.Trim();
        SourceAnalysisReference = sourceAnalysisReference.Trim();
        Status = AnalysisReportStatus.Generated;
        FailureReason = null;
        GeneratedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("Failure reason cannot be empty.", nameof(failureReason));

        Status = AnalysisReportStatus.Failed;
        FailureReason = failureReason.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cria uma nova versão do relatório para regeneração controlada.
    /// Reinicia o status para Pending e incrementa a versão.
    /// </summary>
    public void BumpVersion()
    {
        Status = AnalysisReportStatus.Pending;
        ComponentsSummary = null;
        ArchitecturalRisks = null;
        Recommendations = null;
        SourceAnalysisReference = null;
        FailureReason = null;
        GeneratedAt = null;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }
}
