using AgenticPipeline.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace AgenticPipeline.Infrastructure.Data;

/// <summary>
/// Database context for Agentic Pipeline application
/// </summary>
public class AgenticPipelineDbContext : DbContext
{
    public AgenticPipelineDbContext(DbContextOptions<AgenticPipelineDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Pipeline failure analyses
    /// </summary>
    public DbSet<FailureEntity> Failures { get; set; }

    /// <summary>
    /// GitHub commits related to failures
    /// </summary>
    public DbSet<CommitEntity> Commits { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure FailureEntity
        modelBuilder.Entity<FailureEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Indexes for common queries
            entity.HasIndex(e => e.ErrorType)
                .HasDatabaseName("IX_Failures_ErrorType");

            entity.HasIndex(e => e.AnalyzedAt)
                .HasDatabaseName("IX_Failures_AnalyzedAt");

            entity.HasIndex(e => e.ConfidenceScore)
                .HasDatabaseName("IX_Failures_ConfidenceScore");

            entity.HasIndex(e => e.Repository)
                .HasDatabaseName("IX_Failures_Repository");

            // Configure properties
            entity.Property(e => e.LogContent)
                .IsRequired()
                .HasMaxLength(50000); // Max 50K characters for log

            entity.Property(e => e.RootCause)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.DetailedAnalysis)
                .IsRequired()
                .HasMaxLength(5000);

            entity.Property(e => e.ErrorType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.AffectedComponentsJson)
                .IsRequired()
                .HasMaxLength(5000);

            entity.Property(e => e.RecommendationsJson)
                .IsRequired()
                .HasMaxLength(5000);

            entity.Property(e => e.SuspectedCommitsJson)
                .HasMaxLength(10000);

            entity.Property(e => e.Repository)
                .HasMaxLength(200);

            // Relationships
            entity.HasMany(e => e.Commits)
                .WithOne(c => c.Failure)
                .HasForeignKey(c => c.FailureEntityId)
                .OnDelete(DeleteBehavior.Cascade); //Cascade delete means that when a parent record is deleted, all its related child records are automatically deleted as well. 
        });

        // Configure CommitEntity
        modelBuilder.Entity<CommitEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Indexes for common queries
            entity.HasIndex(e => e.Sha)
                .IsUnique()
                .HasDatabaseName("IX_Commits_Sha");

            entity.HasIndex(e => e.CommitDate)
                .HasDatabaseName("IX_Commits_CommitDate");

            entity.HasIndex(e => e.FailureEntityId)
                .HasDatabaseName("IX_Commits_FailureEntityId");

            entity.HasIndex(e => e.Author)
                .HasDatabaseName("IX_Commits_Author");

            // Configure properties
            entity.Property(e => e.Sha)
                .IsRequired()
                .HasMaxLength(40); // Git SHA is 40 characters

            entity.Property(e => e.Author)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.AuthorEmail)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Message)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.FilesChangedJson)
                .IsRequired()
                .HasMaxLength(10000);

            entity.Property(e => e.ChangeType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Url)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.SuspectedReason)
                .HasMaxLength(2000);

            entity.Property(e => e.Evidence)
                .HasMaxLength(5000);
        });
    }
}
