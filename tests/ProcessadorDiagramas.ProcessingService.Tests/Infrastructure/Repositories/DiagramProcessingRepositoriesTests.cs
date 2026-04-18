using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Data;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Data.Repositories;

namespace ProcessadorDiagramas.ProcessingService.Tests.Infrastructure.Repositories;

public sealed class DiagramProcessingRepositoriesTests
{
    [Fact]
    public async Task JobRepository_AddAndGetByAnalysisProcessId_ShouldPersistEntity()
    {
        await using var context = CreateContext();
        var repository = new DiagramProcessingJobRepository(context);
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "uploads/diagram.png", "corr-123");

        await repository.AddAsync(job);

        var persisted = await repository.GetByDiagramAnalysisProcessIdAsync(job.DiagramAnalysisProcessId);

        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(job.Id);
        persisted.InputStorageKey.Should().Be("uploads/diagram.png");
    }

    [Fact]
    public async Task ResultRepository_AddAndGetByJobId_ShouldPersistRawOutput()
    {
        await using var context = CreateContext();
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "uploads/diagram.png", "corr-123");
        await context.DiagramProcessingJobs.AddAsync(job);
        await context.SaveChangesAsync();

        var repository = new DiagramProcessingResultRepository(context);
        var result = DiagramProcessingResult.Create(job.Id, "raw-ai-output");

        await repository.AddAsync(result);

        var persisted = await repository.GetByJobIdAsync(job.Id);

        persisted.Should().NotBeNull();
        persisted!.RawAiOutput.Should().Be("raw-ai-output");
    }

    [Fact]
    public async Task AttemptRepository_AddAndListByJobId_ShouldReturnOrderedAttempts()
    {
        await using var context = CreateContext();
        var job = DiagramProcessingJob.Create(Guid.NewGuid(), "uploads/diagram.png", "corr-123");
        await context.DiagramProcessingJobs.AddAsync(job);
        await context.SaveChangesAsync();

        var repository = new DiagramProcessingAttemptRepository(context);
        var second = DiagramProcessingAttempt.Start(job.Id, 2);
        var first = DiagramProcessingAttempt.Start(job.Id, 1);

        await repository.AddAsync(second);
        await repository.AddAsync(first);

        var persisted = await repository.ListByJobIdAsync(job.Id);

        persisted.Should().HaveCount(2);
        persisted.Select(current => current.AttemptNumber).Should().ContainInOrder(1, 2);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }
}