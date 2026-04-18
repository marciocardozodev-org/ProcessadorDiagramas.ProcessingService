using FluentAssertions;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;

namespace ProcessadorDiagramas.ProcessingService.Tests.Domain;

public sealed class DiagramProcessingResultTests
{
    [Fact]
    public void Create_ValidInput_ReturnsResult()
    {
        var jobId = Guid.NewGuid();

        var result = DiagramProcessingResult.Create(jobId, "{\"raw\":\"output\"}");

        result.Id.Should().NotBeEmpty();
        result.DiagramProcessingJobId.Should().Be(jobId);
        result.RawAiOutput.Should().Be("{\"raw\":\"output\"}");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}