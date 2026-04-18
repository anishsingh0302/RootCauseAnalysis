using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Data;
using AgenticPipeline.Tests.Common;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AgenticPipeline.IntegrationTests;

/// <summary>
/// Integration tests for Database operations with SQLite
/// </summary>
[Trait("Category", "Integration")]
[Trait("RequiresDatabase", "true")]
public class DatabaseIntegrationTests : IntegrationTestBase, IDisposable
{
    private readonly AgenticPipelineDbContext _context;

    public DatabaseIntegrationTests(ITestOutputHelper output) : base(output)
    {
        // Create unique database for each test run
        var dbName = $"test_pipeline_{Guid.NewGuid()}.db";
        var options = new DbContextOptionsBuilder<AgenticPipelineDbContext>()
            .UseSqlite($"Data Source={dbName}")
            .Options;

        _context = new AgenticPipelineDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task SaveFailureEntity_WithValidData_SavesSuccessfully()
    {
        // Arrange: Setup test data for a failure entity
        WriteSection("Testing FailureEntity Save");

        var failure = new FailureEntity
        {
            LogContent = "Error: Compilation failed in Program.cs line 42",
            RootCause = "Missing semicolon in Program.cs",
            DetailedAnalysis = "The compilation error is caused by a missing semicolon at line 42 in Program.cs",
            ErrorType = "CompilationError",
            ConfidenceScore = 0.95,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = JsonSerializer.Serialize(new[] { "Program.cs" }),
            RecommendationsJson = JsonSerializer.Serialize(new[] { "Add semicolon at line 42" }),
            InputTokens = 150,
            OutputTokens = 80,
            Repository = "anishsingh0302/Semantic-Kernel",
            CommitsAnalyzed = 3
        };

        // Act: Save the failure entity to database
        _context.Failures.Add(failure);
        await _context.SaveChangesAsync();

        // Assert: Verify the entity was saved with a valid ID
        Assert.NotEqual(0, failure.Id);
        WriteLine($"Saved failure with ID: {failure.Id}");
        WriteLine($"Root Cause: {failure.RootCause}");
        WriteLine($"Confidence: {failure.ConfidenceScore:P0}");
    }

    [Fact]
    public async Task SaveCommitEntity_WithValidData_SavesSuccessfully()
    {
        // Arrange: Setup test data for a commit entity
        WriteSection("Testing CommitEntity Save");

        var commit = new CommitEntity
        {
            Sha = "abc123def456abc123def456abc123def456abc1",
            Author = "John Doe",
            AuthorEmail = "john@example.com",
            CommitDate = DateTime.UtcNow.AddHours(-2),
            Message = "Refactored authentication module",
            FilesChangedJson = JsonSerializer.Serialize(new[] { "Auth.cs", "Login.cs" }),
            ChangeType = "modified",
            Additions = 50,
            Deletions = 20,
            Url = "https://github.com/test/repo/commit/abc123",
            ConfidenceScore = 0.85,
            SuspectedReason = "Modified authentication which may affect login",
            Evidence = "Error mentions 'AuthService' which was changed"
        };

        // Act: Save the commit entity to database
        _context.Commits.Add(commit);
        await _context.SaveChangesAsync();

        // Assert: Verify the entity was saved with a valid ID
        Assert.NotEqual(0, commit.Id);
        WriteLine($"Saved commit with ID: {commit.Id}");
        WriteLine($"  SHA: {commit.Sha[..7]}");
        WriteLine($"  Author: {commit.Author}");
    }

    [Fact]
    public async Task SaveFailureWithCommits_WithRelationship_SavesSuccessfully()
    {
        // Arrange: Setup test data with failure and multiple related commits
        WriteSection("Testing Failure with Related Commits");

        var failure = new FailureEntity
        {
            LogContent = "Build failed: NullReferenceException",
            RootCause = "Null reference in authentication service",
            DetailedAnalysis = "The service attempts to access a null object",
            ErrorType = "RuntimeError",
            ConfidenceScore = 0.88,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = JsonSerializer.Serialize(new[] { "AuthService.cs" }),
            RecommendationsJson = JsonSerializer.Serialize(new[] { "Add null check" }),
            InputTokens = 200,
            OutputTokens = 100
        };

        var commit1 = new CommitEntity
        {
            Sha = "commit1abc123def456abc123def456abc123de",
            Author = "Alice",
            AuthorEmail = "alice@example.com",
            CommitDate = DateTime.UtcNow.AddHours(-3),
            Message = "Updated auth logic",
            FilesChangedJson = JsonSerializer.Serialize(new[] { "AuthService.cs" }),
            ChangeType = "modified",
            Additions = 30,
            Deletions = 10,
            Url = "https://github.com/test/repo/commit/commit1",
            Failure = failure
        };

        var commit2 = new CommitEntity
        {
            Sha = "commit2def456abc789def456abc789def456ab",
            Author = "Bob",
            AuthorEmail = "bob@example.com",
            CommitDate = DateTime.UtcNow.AddHours(-1),
            Message = "Fixed bug in login",
            FilesChangedJson = JsonSerializer.Serialize(new[] { "Login.cs" }),
            ChangeType = "modified",
            Additions = 15,
            Deletions = 5,
            Url = "https://github.com/test/repo/commit/commit2",
            Failure = failure
        };

        // Act: Save failure with related commits to database
        _context.Failures.Add(failure);
        _context.Commits.AddRange(commit1, commit2);
        await _context.SaveChangesAsync();

        // Assert: Verify failure and related commits were saved correctly
        Assert.NotEqual(0, failure.Id);
        Assert.Equal(2, failure.Commits.Count);
        WriteLine($"Saved failure with {failure.Commits.Count} related commits");
        WriteLine($"  Failure ID: {failure.Id}");
        WriteLine($"  Commit 1: {commit1.Sha[..7]} by {commit1.Author}");
        WriteLine($"  Commit 2: {commit2.Sha[..7]} by {commit2.Author}");
    }

    [Fact]
    public async Task QueryFailures_WithIncludeCommits_ReturnsRelatedData()
    {
        // Arrange: Setup test data and save to database
        WriteSection("Testing Query with Include");

        var failure = new FailureEntity
        {
            LogContent = "Test failure log",
            RootCause = "Test root cause",
            DetailedAnalysis = "Test analysis",
            ErrorType = "TestFailure",
            ConfidenceScore = 0.75,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 100,
            OutputTokens = 50
        };

        var commit = new CommitEntity
        {
            Sha = "testsha123456789012345678901234567890",
            Author = "Tester",
            AuthorEmail = "test@example.com",
            CommitDate = DateTime.UtcNow,
            Message = "Test commit",
            FilesChangedJson = "[]",
            ChangeType = "added",
            Additions = 10,
            Deletions = 0,
            Url = "https://github.com/test/repo/commit/test",
            Failure = failure
        };

        _context.Failures.Add(failure);
        _context.Commits.Add(commit);
        await _context.SaveChangesAsync();

        // Act: Query failure with eager loading of related commits
        var result = await _context.Failures
            .Include(f => f.Commits)
            .FirstOrDefaultAsync(f => f.Id == failure.Id);

        // Assert: Verify failure and related commits were loaded
        Assert.NotNull(result);
        Assert.Single(result.Commits);
        Assert.Equal(commit.Sha, result.Commits.First().Sha);
        WriteLine($"Retrieved failure with {result.Commits.Count} commit(s)");
        WriteLine($"  Commit SHA: {result.Commits.First().Sha[..7]}");
    }

    [Fact]
    public async Task QueryFailuresByErrorType_ReturnsMatchingRecords()
    {
        // Arrange: Setup multiple failures with different error types
        WriteSection("Testing Query by Error Type");

        var failure1 = new FailureEntity
        {
            LogContent = "Compilation error log 1",
            RootCause = "Syntax error",
            DetailedAnalysis = "Missing brace",
            ErrorType = "CompilationError",
            ConfidenceScore = 0.90,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 100,
            OutputTokens = 50
        };

        var failure2 = new FailureEntity
        {
            LogContent = "Test failure log 2",
            RootCause = "Assertion failed",
            DetailedAnalysis = "Expected value mismatch",
            ErrorType = "TestFailure",
            ConfidenceScore = 0.85,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 120,
            OutputTokens = 60
        };

        var failure3 = new FailureEntity
        {
            LogContent = "Compilation error log 3",
            RootCause = "Type mismatch",
            DetailedAnalysis = "Wrong type assignment",
            ErrorType = "CompilationError",
            ConfidenceScore = 0.92,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 110,
            OutputTokens = 55
        };

        _context.Failures.AddRange(failure1, failure2, failure3);
        await _context.SaveChangesAsync();

        // Act: Query failures filtered by error type
        var compilationErrors = await _context.Failures
            .Where(f => f.ErrorType == "CompilationError")
            .ToListAsync();

        // Assert: Verify only compilation errors were returned
        Assert.Equal(2, compilationErrors.Count);
        Assert.All(compilationErrors, f => Assert.Equal("CompilationError", f.ErrorType));
        WriteLine($"Found {compilationErrors.Count} compilation errors");
    }

    [Fact]
    public async Task QueryCommitBySha_WithUniqueConstraint_ReturnsExactMatch()
    {
        // Arrange: Setup commit with unique SHA
        WriteSection("Testing Query Commit by SHA");

        var targetSha = "uniquesha123456789012345678901234567";
        var commit = new CommitEntity
        {
            Sha = targetSha,
            Author = "Developer",
            AuthorEmail = "dev@example.com",
            CommitDate = DateTime.UtcNow,
            Message = "Important fix",
            FilesChangedJson = "[]",
            ChangeType = "modified",
            Additions = 20,
            Deletions = 5,
            Url = "https://github.com/test/repo/commit/unique"
        };

        _context.Commits.Add(commit);
        await _context.SaveChangesAsync();

        // Act: Query commit by unique SHA identifier
        var result = await _context.Commits
            .FirstOrDefaultAsync(c => c.Sha == targetSha);

        // Assert: Verify exact commit was found
        Assert.NotNull(result);
        Assert.Equal(targetSha, result.Sha);
        Assert.Equal("Developer", result.Author);
        WriteLine($"Found commit by SHA: {result.Sha[..7]}");
        WriteLine($"  Author: {result.Author}");
        WriteLine($"  Message: {result.Message}");
    }

    [Fact]
    public async Task DeleteFailure_WithCascadeDelete_RemovesRelatedCommits()
    {
        // Arrange: Setup failure with related commit
        WriteSection("Testing Cascade Delete");

        var failure = new FailureEntity
        {
            LogContent = "Delete test log",
            RootCause = "Delete test cause",
            DetailedAnalysis = "Delete test analysis",
            ErrorType = "TestFailure",
            ConfidenceScore = 0.70,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 100,
            OutputTokens = 50
        };

        var commit = new CommitEntity
        {
            Sha = "deletesha1234567890123456789012345678",
            Author = "Deleter",
            AuthorEmail = "delete@example.com",
            CommitDate = DateTime.UtcNow,
            Message = "Commit to be deleted",
            FilesChangedJson = "[]",
            ChangeType = "modified",
            Additions = 10,
            Deletions = 0,
            Url = "https://github.com/test/repo/commit/delete",
            Failure = failure
        };

        _context.Failures.Add(failure);
        _context.Commits.Add(commit);
        await _context.SaveChangesAsync();

        var failureId = failure.Id;
        var commitId = commit.Id;

        // Act: Delete parent failure (should cascade to related commit)
        _context.Failures.Remove(failure);
        await _context.SaveChangesAsync();

        // Assert: Verify both failure and related commit were deleted
        var deletedFailure = await _context.Failures.FindAsync(failureId);
        var deletedCommit = await _context.Commits.FindAsync(commitId);

        Assert.Null(deletedFailure);
        Assert.Null(deletedCommit);
        WriteLine($"Cascade delete successful");
        WriteLine($"  Failure deleted: {failureId}");
        WriteLine($"  Related commit also deleted: {commitId}");
    }

    [Fact]
    public async Task CalculateStatistics_WithMultipleRecords_ReturnsCorrectAggregates()
    {
        // Arrange: Setup multiple failures with different metrics
        WriteSection("Testing Database Statistics");

        var failures = new[]
        {
            new FailureEntity
            {
                LogContent = "Log 1",
                RootCause = "Cause 1",
                DetailedAnalysis = "Analysis 1",
                ErrorType = "CompilationError",
                ConfidenceScore = 0.90,
                AnalyzedAt = DateTime.UtcNow,
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 100,
                OutputTokens = 50
            },
            new FailureEntity
            {
                LogContent = "Log 2",
                RootCause = "Cause 2",
                DetailedAnalysis = "Analysis 2",
                ErrorType = "TestFailure",
                ConfidenceScore = 0.80,
                AnalyzedAt = DateTime.UtcNow,
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 150,
                OutputTokens = 75
            },
            new FailureEntity
            {
                LogContent = "Log 3",
                RootCause = "Cause 3",
                DetailedAnalysis = "Analysis 3",
                ErrorType = "DependencyIssue",
                ConfidenceScore = 0.70,
                AnalyzedAt = DateTime.UtcNow,
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 120,
                OutputTokens = 60
            }
        };

        _context.Failures.AddRange(failures);
        await _context.SaveChangesAsync();

        // Act: Calculate aggregate statistics from database
        var totalCount = await _context.Failures.CountAsync();
        var avgConfidence = await _context.Failures.AverageAsync(f => f.ConfidenceScore);
        var totalInputTokens = await _context.Failures.SumAsync(f => f.InputTokens);
        var totalOutputTokens = await _context.Failures.SumAsync(f => f.OutputTokens);

        // Assert: Verify all statistics are calculated correctly
        Assert.Equal(3, totalCount);
        Assert.Equal(0.80, avgConfidence, 2);
        Assert.Equal(370, totalInputTokens);
        Assert.Equal(185, totalOutputTokens);
        WriteLine($"Statistics calculated correctly");
        WriteLine($"  Total Failures: {totalCount}");
        WriteLine($"  Avg Confidence: {avgConfidence:P0}");
        WriteLine($"  Total Input Tokens: {totalInputTokens}");
        WriteLine($"  Total Output Tokens: {totalOutputTokens}");
    }

    [Fact]
    public async Task QueryFailuresByDateRange_ReturnsRecordsInRange()
    {
        // Arrange: Setup failures with different analysis dates
        WriteSection("Testing Query by Date Range");

        var now = DateTime.UtcNow;
        var failures = new[]
        {
            new FailureEntity
            {
                LogContent = "Old log",
                RootCause = "Old cause",
                DetailedAnalysis = "Old analysis",
                ErrorType = "CompilationError",
                ConfidenceScore = 0.85,
                AnalyzedAt = now.AddDays(-5),
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 100,
                OutputTokens = 50
            },
            new FailureEntity
            {
                LogContent = "Recent log 1",
                RootCause = "Recent cause 1",
                DetailedAnalysis = "Recent analysis 1",
                ErrorType = "TestFailure",
                ConfidenceScore = 0.90,
                AnalyzedAt = now.AddHours(-12),
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 120,
                OutputTokens = 60
            },
            new FailureEntity
            {
                LogContent = "Recent log 2",
                RootCause = "Recent cause 2",
                DetailedAnalysis = "Recent analysis 2",
                ErrorType = "RuntimeError",
                ConfidenceScore = 0.88,
                AnalyzedAt = now.AddHours(-6),
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 110,
                OutputTokens = 55
            }
        };

        _context.Failures.AddRange(failures);
        await _context.SaveChangesAsync();

        // Act: Query failures within specific date range
        var startDate = now.AddDays(-1);
        var endDate = now;
        var recentFailures = await _context.Failures
            .Where(f => f.AnalyzedAt >= startDate && f.AnalyzedAt <= endDate)
            .ToListAsync();

        // Assert: Verify only failures within date range were returned
        Assert.Equal(2, recentFailures.Count);
        Assert.All(recentFailures, f =>
        {
            Assert.True(f.AnalyzedAt >= startDate);
            Assert.True(f.AnalyzedAt <= endDate);
        });
        WriteLine($"Found {recentFailures.Count} failures in last 24 hours");
    }
}
