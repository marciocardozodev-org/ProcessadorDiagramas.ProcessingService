using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.ReportingService.Domain.Entities;
using ProcessadorDiagramas.ReportingService.Domain.Enums;
using ProcessadorDiagramas.ReportingService.Infrastructure.Data;
using ProcessadorDiagramas.ReportingService.Infrastructure.Data.Repositories;

namespace ProcessadorDiagramas.ReportingService.Tests.Infrastructure.Repositories;

public sealed class AnalysisReportRepositoryTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    [Fact]
    public async Task AddAsync_AndGetByAnalysisProcessId_ShouldPersistReport()
    {
        await using var context = CreateContext();
        var repository = new AnalysisReportRepository(context);
        var report = AnalysisReport.CreatePending(Guid.NewGuid());

        await repository.AddAsync(report);

        var persisted = await repository.GetByAnalysisProcessIdAsync(report.AnalysisProcessId);

        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(report.Id);
        persisted.Status.Should().Be(AnalysisReportStatus.Pending);
        persisted.Version.Should().Be(1);
    }

    [Fact]
    public async Task AddAsync_AndGetById_ShouldReturnCorrectReport()
    {
        await using var context = CreateContext();
        var repository = new AnalysisReportRepository(context);
        var report = AnalysisReport.CreatePending(Guid.NewGuid());

        await repository.AddAsync(report);

        var persisted = await repository.GetByIdAsync(report.Id);

        persisted.Should().NotBeNull();
        persisted!.AnalysisProcessId.Should().Be(report.AnalysisProcessId);
    }

    [Fact]
    public async Task GetByAnalysisProcessIdAsync_WhenNotExists_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new AnalysisReportRepository(context);

        var result = await repository.GetByAnalysisProcessIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new AnalysisReportRepository(context);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_PersistsGeneratedReport()
    {
        await using var context = CreateContext();
        var repository = new AnalysisReportRepository(context);
        var report = AnalysisReport.CreatePending(Guid.NewGuid());
        await repository.AddAsync(report);

        report.MarkAsGenerated("comps", "risks", "recs", "job-ref-123");
        await repository.UpdateAsync(report);

        var persisted = await repository.GetByIdAsync(report.Id);

        persisted!.Status.Should().Be(AnalysisReportStatus.Generated);
        persisted.ComponentsSummary.Should().Be("comps");
        persisted.ArchitecturalRisks.Should().Be("risks");
        persisted.Recommendations.Should().Be("recs");
        persisted.SourceAnalysisReference.Should().Be("job-ref-123");
        persisted.GeneratedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_PersistsFailedReport()
    {
        await using var context = CreateContext();
        var repository = new AnalysisReportRepository(context);
        var report = AnalysisReport.CreatePending(Guid.NewGuid());
        await repository.AddAsync(report);

        report.MarkAsFailed("ProcessingService unreachable.");
        await repository.UpdateAsync(report);

        var persisted = await repository.GetByIdAsync(report.Id);

        persisted!.Status.Should().Be(AnalysisReportStatus.Failed);
        persisted.FailureReason.Should().Be("ProcessingService unreachable.");
    }

    [Fact]
    public async Task UpdateAsync_AfterBumpVersion_PersistsNewVersion()
    {
        await using var context = CreateContext();
        var repository = new AnalysisReportRepository(context);
        var report = AnalysisReport.CreatePending(Guid.NewGuid());
        report.MarkAsGenerated("comps", "risks", "recs", "ref");
        await repository.AddAsync(report);

        report.BumpVersion();
        report.MarkAsGenerated("new-comps", "new-risks", "new-recs", "new-ref");
        await repository.UpdateAsync(report);

        var persisted = await repository.GetByIdAsync(report.Id);

        persisted!.Version.Should().Be(2);
        persisted.ComponentsSummary.Should().Be("new-comps");
    }
}
