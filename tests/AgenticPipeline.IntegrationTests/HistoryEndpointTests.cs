using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Data;
using AgenticPipeline.Infrastructure.Repositories;
using AgenticPipeline.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AgenticPipeline.IntegrationTests;

/// <summary>
/// Integration tests for history retrieval endpoints
/// </summary>
public class HistoryEndpointTests : IntegrationTestBase, IDisposable
{
    private readonly AgenticPipelineDbContext _context;
    private readonly IFailureRepository _repository;

    public HistoryEndpointTests(ITestOutputHelper output) : base(output)
    {
        // Arrange: Setup unique test database
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_history_{Guid.NewGuid()}.db");
        var connectionString = $"Data Source={dbPath}";

        var optionsBuilder = new DbContextOptionsBuilder<AgenticPipelineDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        _context = new AgenticPipelineDbContext(optionsBuilder.Options);
        _context.Database.EnsureCreated();

        var repoLogger = LoggerFactory.CreateLogger<FailureRepository>();
        var mockEmbeddingService = new MockEmbeddingService();
        _repository = new FailureRepository(_context, repoLogger, mockEmbeddingService);
    }

    [Fact]
    public async Task GetHistory_WithNoData_ReturnsEmptyList()
    {
        // Arrange: Database is empty by default

        // Act: Retrieve history from empty database
        var history = await _repository.GetHistoryAsync(limit: 10);

        // Assert: Verify empty list is returned
        Assert.NotNull(history);
        Assert.Empty(history);
        Output.WriteLine("Empty history returned successfully");
    }

    [Fact]
    public async Task GetHistory_WithMultipleFailures_ReturnsMostRecentFirst()
    {
        // Arrange: Create three failures with different timestamps
        var failure1 = new FailureEntity
        {
            LogContent = "Error 1",
            RootCause = "Cause 1",
            DetailedAnalysis = "Analysis 1",
            ErrorType = "NullReference",
            ConfidenceScore = 0.8,
            AnalyzedAt = DateTime.UtcNow.AddHours(-10),
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 100,
            OutputTokens = 50
        };

        var failure2 = new FailureEntity
        {
            LogContent = "Error 2",
            RootCause = "Cause 2",
            DetailedAnalysis = "Analysis 2",
            ErrorType = "Timeout",
            ConfidenceScore = 0.9,
            AnalyzedAt = DateTime.UtcNow.AddHours(-5),
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 200,
            OutputTokens = 100
        };

        var failure3 = new FailureEntity
        {
            LogContent = "Error 3",
            RootCause = "Cause 3",
            DetailedAnalysis = "Analysis 3",
            ErrorType = "NullReference",
            ConfidenceScore = 0.85,
            AnalyzedAt = DateTime.UtcNow.AddHours(-1),
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 150,
            OutputTokens = 75
        };

        await _repository.SaveFailureAsync(failure1);
        await _repository.SaveFailureAsync(failure2);
        await _repository.SaveFailureAsync(failure3);

        // Act: Retrieve history
        var history = await _repository.GetHistoryAsync(limit: 10);

        // Assert: Verify results are ordered by most recent first
        Assert.Equal(3, history.Count);
        Assert.True(history[0].AnalyzedAt >= history[1].AnalyzedAt);
        Assert.True(history[1].AnalyzedAt >= history[2].AnalyzedAt);
        Assert.Equal("Error 3", history[0].LogContent);
        Output.WriteLine($"Retrieved {history.Count} failures in correct order");
    }

    [Fact]
    public async Task GetHistory_WithPagination_ReturnsCorrectSubset()
    {
        // Arrange: Create 5 failures and save them
        for (int i = 1; i <= 5; i++)
        {
            var failure = new FailureEntity
            {
                LogContent = $"Error {i}",
                RootCause = $"Cause {i}",
                DetailedAnalysis = $"Analysis {i}",
                ErrorType = "TestError",
                ConfidenceScore = 0.8 + (i * 0.01),
                AnalyzedAt = DateTime.UtcNow.AddHours(-i),
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 100 * i,
                OutputTokens = 50 * i
            };
            await _repository.SaveFailureAsync(failure);
        }

        // Act: Retrieve second page with limit of 2
        var history = await _repository.GetHistoryAsync(limit: 2, offset: 2);

        // Assert: Verify correct subset is returned
        Assert.Equal(2, history.Count);
        Assert.Equal("Error 3", history[0].LogContent);
        Assert.Equal("Error 4", history[1].LogContent);
        Output.WriteLine($"Pagination returned correct subset: {history.Count} items");
    }

    [Fact]
    public async Task GetHistory_WithErrorTypeFilter_ReturnsOnlyMatchingFailures()
    {
        // Arrange: Create failures with different error types
        var nullRefFailure = new FailureEntity
        {
            LogContent = "NullRef Error",
            RootCause = "Null pointer",
            DetailedAnalysis = "Object was null",
            ErrorType = "NullReferenceException",
            ConfidenceScore = 0.9,
            AnalyzedAt = DateTime.UtcNow.AddHours(-2),
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 100,
            OutputTokens = 50
        };

        var timeoutFailure = new FailureEntity
        {
            LogContent = "Timeout Error",
            RootCause = "Connection timeout",
            DetailedAnalysis = "Request timed out",
            ErrorType = "TimeoutException",
            ConfidenceScore = 0.85,
            AnalyzedAt = DateTime.UtcNow.AddHours(-1),
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 150,
            OutputTokens = 75
        };

        await _repository.SaveFailureAsync(nullRefFailure);
        await _repository.SaveFailureAsync(timeoutFailure);

        // Act: Retrieve history filtered by NullReferenceException
        var history = await _repository.GetHistoryAsync(limit: 10, errorType: "NullReferenceException");

        // Assert: Verify only NullReferenceException failures are returned
        Assert.Single(history);
        Assert.Equal("NullReferenceException", history[0].ErrorType);
        Assert.Equal("NullRef Error", history[0].LogContent);
        Output.WriteLine($"Filter returned {history.Count} matching failure(s)");
    }

    [Fact]
    public async Task GetById_WithValidId_ReturnsFailureWithCommits()
    {
        // Arrange: Create failure with commits
        var failure = new FailureEntity
        {
            LogContent = "Test Error",
            RootCause = "Test Cause",
            DetailedAnalysis = "Test Analysis",
            ErrorType = "TestException",
            ConfidenceScore = 0.92,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 200,
            OutputTokens = 100
        };

        var commits = new List<CommitEntity>
        {
            new CommitEntity
            {
                Sha = "abc123",
                Author = "Test Author",
                CommitDate = DateTime.UtcNow.AddHours(-3),
                Message = "Test commit",
                FilesChangedJson = "[\"test.cs\"]",
                ConfidenceScore = 0.88,
                SuspectedReason = "Modified test file"
            }
        };

        var savedFailure = await _repository.SaveFailureWithCommitsAsync(failure, commits);

        // Act: Retrieve by ID with commits
        var retrieved = await _repository.GetByIdAsync(savedFailure.Id, includeCommits: true);

        // Assert: Verify failure and commits are retrieved correctly
        Assert.NotNull(retrieved);
        Assert.Equal(savedFailure.Id, retrieved.Id);
        Assert.Equal("Test Error", retrieved.LogContent);
        Assert.Single(retrieved.Commits);
        Assert.Equal("abc123", retrieved.Commits.First().Sha);
        Output.WriteLine($"Retrieved failure {retrieved.Id} with {retrieved.Commits.Count} commit(s)");
    }

    [Fact]
    public async Task GetById_WithInvalidId_ReturnsNull()
    {
        // Arrange: Use an ID that doesn't exist
        var nonExistentId = 99999;

        // Act: Attempt to retrieve non-existent failure
        var retrieved = await _repository.GetByIdAsync(nonExistentId);

        // Assert: Verify null is returned
        Assert.Null(retrieved);
        Output.WriteLine($"Non-existent ID {nonExistentId} correctly returned null");
    }

    [Fact]
    public async Task GetStatistics_WithMultipleFailures_CalculatesCorrectAggregates()
    {
        // Arrange: Create multiple failures with known values
        var failure1 = new FailureEntity
        {
            LogContent = "Error 1",
            RootCause = "Cause 1",
            DetailedAnalysis = "Analysis 1",
            ErrorType = "Type1",
            ConfidenceScore = 0.8,
            AnalyzedAt = DateTime.UtcNow.AddHours(-5),
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 100,
            OutputTokens = 50
        };

        var failure2 = new FailureEntity
        {
            LogContent = "Error 2",
            RootCause = "Cause 2",
            DetailedAnalysis = "Analysis 2",
            ErrorType = "Type2",
            ConfidenceScore = 0.9,
            AnalyzedAt = DateTime.UtcNow.AddHours(-2),
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 200,
            OutputTokens = 100
        };

        var failure3 = new FailureEntity
        {
            LogContent = "Error 3",
            RootCause = "Cause 3",
            DetailedAnalysis = "Analysis 3",
            ErrorType = "Type1",
            ConfidenceScore = 0.7,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 150,
            OutputTokens = 75
        };

        await _repository.SaveFailureAsync(failure1);
        await _repository.SaveFailureAsync(failure2);
        await _repository.SaveFailureAsync(failure3);

        // Act: Calculate statistics
        var stats = await _repository.GetStatisticsAsync();

        // Assert: Verify statistics are calculated correctly
        Assert.Equal(3, stats.TotalFailures);
        Assert.Equal(0.8, stats.AverageConfidence, precision: 2);
        Assert.Equal(450, stats.TotalInputTokens);
        Assert.Equal(225, stats.TotalOutputTokens);
        Assert.Equal(2, stats.FailuresByErrorType["Type1"]);
        Assert.Equal(1, stats.FailuresByErrorType["Type2"]);
        Assert.NotNull(stats.MostRecentAnalysis);
        Assert.NotNull(stats.OldestAnalysis);
        Output.WriteLine($"Statistics: {stats.TotalFailures} failures, {stats.AverageConfidence:P0} avg confidence");
    }

    [Fact]
    public async Task GetStatistics_WithEmptyDatabase_ReturnsZeroStatistics()
    {
        // Arrange: Database is empty by default

        // Act: Calculate statistics on empty database
        var stats = await _repository.GetStatisticsAsync();

        // Assert: Verify zero statistics are returned
        Assert.Equal(0, stats.TotalFailures);
        Assert.Equal(0, stats.AverageConfidence);
        Assert.Equal(0, stats.TotalInputTokens);
        Assert.Equal(0, stats.TotalOutputTokens);
        Assert.Empty(stats.FailuresByErrorType);
        Assert.Null(stats.MostRecentAnalysis);
        Assert.Null(stats.OldestAnalysis);
        Output.WriteLine("Empty database returned zero statistics correctly");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
