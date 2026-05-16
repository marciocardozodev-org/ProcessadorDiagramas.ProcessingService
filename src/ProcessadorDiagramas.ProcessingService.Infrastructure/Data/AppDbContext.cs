using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.ProcessingService.Domain.Entities;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<DiagramProcessingJob> DiagramProcessingJobs => Set<DiagramProcessingJob>();

    public DbSet<DiagramProcessingResult> DiagramProcessingResults => Set<DiagramProcessingResult>();

    public DbSet<DiagramProcessingAttempt> DiagramProcessingAttempts => Set<DiagramProcessingAttempt>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DiagramProcessingJob>(entity =>
        {
            entity.HasKey(current => current.Id);
            entity.Property(current => current.InputStorageKey).IsRequired().HasMaxLength(2048);
            entity.Property(current => current.PreprocessedContent).HasMaxLength(20000);
            entity.Property(current => current.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(current => current.FailureReason).HasMaxLength(2000);
            entity.Property(current => current.CorrelationId).IsRequired().HasMaxLength(100);
            entity.Property(current => current.RequestId).IsRequired().HasMaxLength(100);
            entity.HasIndex(current => current.DiagramAnalysisProcessId).IsUnique();
            entity.HasIndex(current => current.CorrelationId);
            entity.HasIndex(current => current.RequestId);
        });

        modelBuilder.Entity<DiagramProcessingResult>(entity =>
        {
            entity.HasKey(current => current.Id);
            entity.Property(current => current.RawAiOutput).IsRequired();
            entity.HasIndex(current => current.DiagramProcessingJobId).IsUnique();
            entity.HasOne<DiagramProcessingJob>()
                .WithMany()
                .HasForeignKey(current => current.DiagramProcessingJobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DiagramProcessingAttempt>(entity =>
        {
            entity.HasKey(current => current.Id);
            entity.Property(current => current.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(current => current.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(current => new { current.DiagramProcessingJobId, current.AttemptNumber }).IsUnique();
            entity.HasOne<DiagramProcessingJob>()
                .WithMany()
                .HasForeignKey(current => current.DiagramProcessingJobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(current => current.Id);
            entity.Property(current => current.EventType).IsRequired().HasMaxLength(200);
            entity.Property(current => current.Payload).IsRequired();
            entity.Property(current => current.CorrelationId).IsRequired().HasMaxLength(100);
            entity.Property(current => current.RequestId).IsRequired().HasMaxLength(100);
            entity.Property(current => current.LastError).HasMaxLength(2000);
            entity.HasIndex(current => current.ProcessedAtUtc);
            entity.HasIndex(current => current.CreatedAtUtc);
            entity.HasIndex(current => current.RequestId);
        });
    }
}