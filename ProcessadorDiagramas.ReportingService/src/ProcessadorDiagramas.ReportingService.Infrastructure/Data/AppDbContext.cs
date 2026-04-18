using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.ReportingService.Domain.Entities;

namespace ProcessadorDiagramas.ReportingService.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AnalysisReport> AnalysisReports => Set<AnalysisReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalysisReport>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(r => r.ComponentsSummary).HasColumnType("text");
            entity.Property(r => r.ArchitecturalRisks).HasColumnType("text");
            entity.Property(r => r.Recommendations).HasColumnType("text");
            entity.Property(r => r.SourceAnalysisReference).HasMaxLength(500);
            entity.Property(r => r.FailureReason).HasMaxLength(2000);
            entity.HasIndex(r => r.AnalysisProcessId).IsUnique();
        });
    }
}
