using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProcessadorDiagramas.ReportingService.Application.Contracts;
using ProcessadorDiagramas.ReportingService.Application.Interfaces;
using ProcessadorDiagramas.ReportingService.Application.Queries.GetOrGenerateAnalysisReport;
using ProcessadorDiagramas.ReportingService.Domain.Entities;
using ProcessadorDiagramas.ReportingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ReportingService.Tests.Application;

public sealed class GetOrGenerateAnalysisReportQueryHandlerTests
{
    private readonly Mock<IAnalysisReportRepository> _repositoryMock = new();
    private readonly Mock<IProcessingServiceClient> _clientMock = new();
    private readonly GetOrGenerateAnalysisReportQueryHandler _handler;

    public GetOrGenerateAnalysisReportQueryHandlerTests()
    {
        _handler = new GetOrGenerateAnalysisReportQueryHandler(
            _repositoryMock.Object,
            _clientMock.Object,
            NullLogger<GetOrGenerateAnalysisReportQueryHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ReportAlreadyGenerated_ReturnsCachedReport()
    {
        var analysisProcessId = Guid.NewGuid();
        var report = AnalysisReport.CreatePending(analysisProcessId);
        report.MarkAsGenerated("comps", "risks", "recs", "job-ref");

        _repositoryMock
            .Setup(r => r.GetByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync(report);

        var result = await _handler.HandleAsync(
            new GetOrGenerateAnalysisReportQuery(analysisProcessId));

        result.Should().NotBeNull();
        result!.Status.Should().Be("Generated");
        result.ComponentsSummary.Should().Be("comps");
        _clientMock.Verify(c => c.GetJobByAnalysisProcessIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ProcessingServiceReturnsNull_ReturnsNull()
    {
        var analysisProcessId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync((AnalysisReport?)null);

        _clientMock
            .Setup(c => c.GetJobByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync((ProcessingJobResult?)null);

        var result = await _handler.HandleAsync(
            new GetOrGenerateAnalysisReportQuery(analysisProcessId));

        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_JobNotCompleted_ReturnsPendingReport()
    {
        var analysisProcessId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync((AnalysisReport?)null);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<AnalysisReport>(), default))
            .Returns(Task.CompletedTask);

        _clientMock
            .Setup(c => c.GetJobByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync(new ProcessingJobResult(Guid.NewGuid(), analysisProcessId, "InProgress", null, null));

        var result = await _handler.HandleAsync(
            new GetOrGenerateAnalysisReportQuery(analysisProcessId));

        result.Should().NotBeNull();
        result!.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task HandleAsync_JobCompleted_GeneratesAndPersistsReport()
    {
        var analysisProcessId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var rawOutput = """{"components":"[API, DB]","risks":"[no retry policy]","recommendations":"[add circuit breaker]"}""";

        _repositoryMock
            .Setup(r => r.GetByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync((AnalysisReport?)null);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<AnalysisReport>(), default))
            .Returns(Task.CompletedTask);

        _clientMock
            .Setup(c => c.GetJobByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync(new ProcessingJobResult(jobId, analysisProcessId, "Completed", rawOutput, DateTime.UtcNow));

        var result = await _handler.HandleAsync(
            new GetOrGenerateAnalysisReportQuery(analysisProcessId));

        result.Should().NotBeNull();
        result!.Status.Should().Be("Generated");
        result.SourceAnalysisReference.Should().Be(jobId.ToString());
        result.GeneratedAt.Should().NotBeNull();
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<AnalysisReport>(), default), Times.Once);
    }
}
