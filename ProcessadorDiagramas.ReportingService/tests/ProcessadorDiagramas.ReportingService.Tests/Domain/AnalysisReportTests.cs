using FluentAssertions;
using ProcessadorDiagramas.ReportingService.Domain.Entities;
using ProcessadorDiagramas.ReportingService.Domain.Enums;

namespace ProcessadorDiagramas.ReportingService.Tests.Domain;

public sealed class AnalysisReportTests
{
    [Fact]
    public void CreatePending_ValidInput_ReturnsPendingReport()
    {
        var analysisProcessId = Guid.NewGuid();

        var report = AnalysisReport.CreatePending(analysisProcessId);

        report.Id.Should().NotBeEmpty();
        report.AnalysisProcessId.Should().Be(analysisProcessId);
        report.Status.Should().Be(AnalysisReportStatus.Pending);
        report.Version.Should().Be(1);
        report.ComponentsSummary.Should().BeNull();
        report.ArchitecturalRisks.Should().BeNull();
        report.Recommendations.Should().BeNull();
        report.GeneratedAt.Should().BeNull();
        report.FailureReason.Should().BeNull();
        report.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreatePending_EmptyAnalysisProcessId_ThrowsArgumentException()
    {
        var act = () => AnalysisReport.CreatePending(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsGenerated_ValidInput_SetsGeneratedStatus()
    {
        var report = AnalysisReport.CreatePending(Guid.NewGuid());

        report.MarkAsGenerated(
            componentsSummary: "[{\"name\":\"API Gateway\"}]",
            architecturalRisks: "[{\"risk\":\"Single point of failure\"}]",
            recommendations: "[{\"action\":\"Add circuit breaker\"}]",
            sourceAnalysisReference: "job-abc-123");

        report.Status.Should().Be(AnalysisReportStatus.Generated);
        report.ComponentsSummary.Should().Contain("API Gateway");
        report.ArchitecturalRisks.Should().Contain("Single point of failure");
        report.Recommendations.Should().Contain("circuit breaker");
        report.SourceAnalysisReference.Should().Be("job-abc-123");
        report.GeneratedAt.Should().NotBeNull();
        report.UpdatedAt.Should().NotBeNull();
        report.FailureReason.Should().BeNull();
    }

    [Theory]
    [InlineData("", "risks", "recs", "ref")]
    [InlineData("comps", "", "recs", "ref")]
    [InlineData("comps", "risks", "", "ref")]
    [InlineData("comps", "risks", "recs", "")]
    public void MarkAsGenerated_MissingFields_ThrowsArgumentException(
        string components, string risks, string recs, string sourceRef)
    {
        var report = AnalysisReport.CreatePending(Guid.NewGuid());

        var act = () => report.MarkAsGenerated(components, risks, recs, sourceRef);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsFailed_ValidReason_SetsFailedStatus()
    {
        var report = AnalysisReport.CreatePending(Guid.NewGuid());

        report.MarkAsFailed("ProcessingService returned empty result.");

        report.Status.Should().Be(AnalysisReportStatus.Failed);
        report.FailureReason.Should().Be("ProcessingService returned empty result.");
        report.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void BumpVersion_IncreasesVersionAndResetsToPending()
    {
        var report = AnalysisReport.CreatePending(Guid.NewGuid());
        report.MarkAsGenerated("comps", "risks", "recs", "ref");

        report.BumpVersion();

        report.Version.Should().Be(2);
        report.Status.Should().Be(AnalysisReportStatus.Pending);
        report.ComponentsSummary.Should().BeNull();
        report.GeneratedAt.Should().BeNull();
    }
}
