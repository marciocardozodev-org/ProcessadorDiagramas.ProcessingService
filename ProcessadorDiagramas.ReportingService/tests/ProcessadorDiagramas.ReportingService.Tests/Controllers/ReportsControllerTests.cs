using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using ProcessadorDiagramas.ReportingService.Application.Contracts;
using ProcessadorDiagramas.ReportingService.Application.Interfaces;
using ProcessadorDiagramas.ReportingService.Application.Queries.GetOrGenerateAnalysisReport;
using ProcessadorDiagramas.ReportingService.Domain.Entities;
using ProcessadorDiagramas.ReportingService.Domain.Interfaces;
using ProcessadorDiagramas.ReportingService.Infrastructure.Data;

namespace ProcessadorDiagramas.ReportingService.Tests.Controllers;

public sealed class ReportsControllerTests : IClassFixture<ReportsControllerTests.ControllerTestFactory>
{
    private readonly HttpClient _httpClient;
    private readonly Mock<IAnalysisReportRepository> _reportRepositoryMock;
    private readonly Mock<IProcessingServiceClient> _processingClientMock;

    public ReportsControllerTests(ControllerTestFactory factory)
    {
        _httpClient = factory.CreateClient();
        _reportRepositoryMock = factory.ReportRepositoryMock;
        _processingClientMock = factory.ProcessingClientMock;
    }

    // GET /internal/reports/{id} — relatório já gerado (retorna do cache)
    [Fact]
    public async Task GetReport_WhenReportAlreadyGenerated_Returns200()
    {
        var analysisProcessId = Guid.NewGuid();
        var report = AnalysisReport.CreatePending(analysisProcessId);
        report.MarkAsGenerated("[{\"name\":\"API\"}]", "[{\"risk\":\"SPOF\"}]", "[{\"action\":\"add retry\"}]", "job-ref");

        _reportRepositoryMock
            .Setup(r => r.GetByAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var httpResponse = await _httpClient.GetAsync($"/internal/reports/{analysisProcessId}");

        httpResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await httpResponse.Content.ReadFromJsonAsync<GetOrGenerateAnalysisReportResponse>();
        body!.Status.Should().Be("Generated");
        body.AnalysisProcessId.Should().Be(analysisProcessId);
    }

    // GET /internal/reports/{id} — processo não encontrado no ProcessingService
    [Fact]
    public async Task GetReport_WhenProcessNotFoundInProcessingService_Returns404()
    {
        var analysisProcessId = Guid.NewGuid();

        _reportRepositoryMock
            .Setup(r => r.GetByAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnalysisReport?)null);

        _processingClientMock
            .Setup(c => c.GetJobByAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessingJobResult?)null);

        var httpResponse = await _httpClient.GetAsync($"/internal/reports/{analysisProcessId}");

        httpResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // GET /internal/reports/{id} — job ainda em processamento
    [Fact]
    public async Task GetReport_WhenJobNotCompleted_Returns202()
    {
        var analysisProcessId = Guid.NewGuid();

        _reportRepositoryMock
            .Setup(r => r.GetByAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnalysisReport?)null);

        _reportRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<AnalysisReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _processingClientMock
            .Setup(c => c.GetJobByAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessingJobResult(Guid.NewGuid(), analysisProcessId, "InProgress", null, null));

        var httpResponse = await _httpClient.GetAsync($"/internal/reports/{analysisProcessId}");

        httpResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    // POST /internal/reports/{id}/generate — geração bem sucedida
    [Fact]
    public async Task GenerateReport_WhenJobCompleted_Returns200()
    {
        var analysisProcessId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var rawOutput = """{"components":"[API]","risks":"[SPOF]","recommendations":"[add retry]"}""";

        _processingClientMock
            .Setup(c => c.GetJobByAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessingJobResult(jobId, analysisProcessId, "Completed", rawOutput, DateTime.UtcNow));

        _reportRepositoryMock
            .Setup(r => r.GetByAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnalysisReport?)null);

        _reportRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<AnalysisReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var httpResponse = await _httpClient.PostAsync($"/internal/reports/{analysisProcessId}/generate", null);

        httpResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // POST /internal/reports/{id}/generate — processo não encontrado
    [Fact]
    public async Task GenerateReport_WhenProcessNotFound_Returns404()
    {
        var analysisProcessId = Guid.NewGuid();

        _processingClientMock
            .Setup(c => c.GetJobByAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessingJobResult?)null);

        var httpResponse = await _httpClient.PostAsync($"/internal/reports/{analysisProcessId}/generate", null);

        httpResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // POST /internal/reports/{id}/generate — job ainda pendente
    [Fact]
    public async Task GenerateReport_WhenJobNotCompleted_Returns202()
    {
        var analysisProcessId = Guid.NewGuid();

        _processingClientMock
            .Setup(c => c.GetJobByAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessingJobResult(Guid.NewGuid(), analysisProcessId, "Pending", null, null));

        _reportRepositoryMock
            .Setup(r => r.GetByAnalysisProcessIdAsync(analysisProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnalysisReport?)null);

        _reportRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<AnalysisReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var httpResponse = await _httpClient.PostAsync($"/internal/reports/{analysisProcessId}/generate", null);

        httpResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    // Factory dedicada que injeta mocks de repositório e client
    public sealed class ControllerTestFactory : WebApplicationFactory<Program>
    {
        public Mock<IAnalysisReportRepository> ReportRepositoryMock { get; } = new();
        public Mock<IProcessingServiceClient> ProcessingClientMock { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Substitui DbContext por InMemory
                var dbDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (dbDescriptor is not null)
                    services.Remove(dbDescriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("controller-test-db"));

                // Substitui repositório e client por mocks
                var repoDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IAnalysisReportRepository));
                if (repoDescriptor is not null)
                    services.Remove(repoDescriptor);

                var clientDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IProcessingServiceClient));
                if (clientDescriptor is not null)
                    services.Remove(clientDescriptor);

                services.AddScoped(_ => ReportRepositoryMock.Object);
                services.AddScoped(_ => ProcessingClientMock.Object);
            });
        }
    }
}
