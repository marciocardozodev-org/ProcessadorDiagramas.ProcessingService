using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProcessadorDiagramas.ReportingService.Application.Commands.RegenerateAnalysisReport;
using ProcessadorDiagramas.ReportingService.Application.Contracts;
using ProcessadorDiagramas.ReportingService.Application.Interfaces;
using ProcessadorDiagramas.ReportingService.Domain.Entities;
using ProcessadorDiagramas.ReportingService.Domain.Interfaces;

namespace ProcessadorDiagramas.ReportingService.Tests.Application;

public sealed class RegenerateAnalysisReportCommandHandlerTests
{
    private readonly Mock<IAnalysisReportRepository> _repositoryMock = new();
    private readonly Mock<IProcessingServiceClient> _clientMock = new();
    private readonly RegenerateAnalysisReportCommandHandler _handler;

    public RegenerateAnalysisReportCommandHandlerTests()
    {
        _handler = new RegenerateAnalysisReportCommandHandler(
            _repositoryMock.Object,
            _clientMock.Object,
            NullLogger<RegenerateAnalysisReportCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ProcessingServiceReturnsNull_ReturnsNull()
    {
        var analysisProcessId = Guid.NewGuid();

        _clientMock
            .Setup(c => c.GetJobByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync((ProcessingJobResult?)null);

        var result = await _handler.HandleAsync(new RegenerateAnalysisReportCommand(analysisProcessId));

        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_JobNotCompleted_ReturnsPendingAndDoesNotUpdateExisting()
    {
        var analysisProcessId = Guid.NewGuid();
        var existingReport = AnalysisReport.CreatePending(analysisProcessId);
        existingReport.MarkAsGenerated("old", "old", "old", "old-ref");

        _clientMock
            .Setup(c => c.GetJobByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync(new ProcessingJobResult(Guid.NewGuid(), analysisProcessId, "InProgress", null, null));

        _repositoryMock
            .Setup(r => r.GetByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync(existingReport);

        var result = await _handler.HandleAsync(new RegenerateAnalysisReportCommand(analysisProcessId));

        result.Should().NotBeNull();
        result!.Status.Should().Be("Generated");
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<AnalysisReport>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_JobCompleted_GeneratesNewVersionForExistingReport()
    {
        var analysisProcessId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var rawOutput = """{"components":"[API]","risks":"[no backup]","recommendations":"[add backup]"}""";

        var existingReport = AnalysisReport.CreatePending(analysisProcessId);
        existingReport.MarkAsGenerated("old-comps", "old-risks", "old-recs", "old-ref");

        _clientMock
            .Setup(c => c.GetJobByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync(new ProcessingJobResult(jobId, analysisProcessId, "Completed", rawOutput, DateTime.UtcNow));

        _repositoryMock
            .Setup(r => r.GetByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync(existingReport);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<AnalysisReport>(), default))
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new RegenerateAnalysisReportCommand(analysisProcessId));

        result.Should().NotBeNull();
        result!.Status.Should().Be("Generated");
        result.Version.Should().Be(2);
        result.SourceAnalysisReference.Should().Be(jobId.ToString());
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<AnalysisReport>(), default), Times.Once);
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<AnalysisReport>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_JobCompleted_CreatesNewReportWhenNotExists()
    {
        var analysisProcessId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var rawOutput = """{"components":"[DB]","risks":"[no replication]","recommendations":"[add replica]"}""";

        _clientMock
            .Setup(c => c.GetJobByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync(new ProcessingJobResult(jobId, analysisProcessId, "Completed", rawOutput, DateTime.UtcNow));

        _repositoryMock
            .Setup(r => r.GetByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync((AnalysisReport?)null);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<AnalysisReport>(), default))
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new RegenerateAnalysisReportCommand(analysisProcessId));

        result.Should().NotBeNull();
        result!.Status.Should().Be("Generated");
        result.Version.Should().Be(1);
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<AnalysisReport>(), default), Times.Once);
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<AnalysisReport>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_JobNotCompleted_CreatesPendingWhenNotExists()
    {
        var analysisProcessId = Guid.NewGuid();

        _clientMock
            .Setup(c => c.GetJobByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync(new ProcessingJobResult(Guid.NewGuid(), analysisProcessId, "Pending", null, null));

        _repositoryMock
            .Setup(r => r.GetByAnalysisProcessIdAsync(analysisProcessId, default))
            .ReturnsAsync((AnalysisReport?)null);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<AnalysisReport>(), default))
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new RegenerateAnalysisReportCommand(analysisProcessId));

        result.Should().NotBeNull();
        result!.Status.Should().Be("Pending");
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<AnalysisReport>(), default), Times.Once);
    }
}
