using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Data;
using AgenticPipeline.Infrastructure.Repositories;
using AgenticPipeline.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace AgenticPipeline.IntegrationTests;

/// <summary>
/// Performance benchmarking tests for the pipeline system
/// </summary>
public class PerformanceTests : IntegrationTestBase, IDisposable
{
    private readonly AgenticPipelineDbContext _context;
    private readonly IFailureRepository _repository;

    public PerformanceTests(ITestOutputHelper output) : base(output)
    {
        // Arrange: Setup unique test database
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_perf_{Guid.NewGuid()}.db");
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
    public async Task SaveFailure_Performance_CompletesWithinReasonableTime()
    {
        // Arrange: Prepare failure entity
        var failure = new FailureEntity
        {
            LogContent = "Performance test error log content",
            RootCause = "Test root cause",
            DetailedAnalysis = "Detailed analysis for performance testing",
            ErrorType = "PerformanceTest",
            ConfidenceScore = 0.9,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[\"Component1\", \"Component2\"]",
            RecommendationsJson = "[\"Fix1\", \"Fix2\"]",
            InputTokens = 150,
            OutputTokens = 100
        };

        WriteSection("Save Failure Performance Test");

        // Act: Measure save performance
        var stopwatch = Stopwatch.StartNew();
        var saved = await _repository.SaveFailureAsync(failure);
        stopwatch.Stop();

        // Assert: Verify save completes within 100ms
        Assert.NotEqual(0, saved.Id);
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"Save took {stopwatch.ElapsedMilliseconds}ms, expected < 100ms");

        Output.WriteLine($"Save completed in {stopwatch.ElapsedMilliseconds}ms");
        Output.WriteLine($"Performance: {(stopwatch.ElapsedMilliseconds < 50 ? "Excellent" : "Good")}");
    }

    [Fact]
    public async Task GetById_Performance_CompletesWithinReasonableTime()
    {
        // Arrange: Create and save a failure
        var failure = new FailureEntity
        {
            LogContent = "Test log",
            RootCause = "Test cause",
            DetailedAnalysis = "Test analysis",
            ErrorType = "TestType",
            ConfidenceScore = 0.85,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 100,
            OutputTokens = 50
        };
        var saved = await _repository.SaveFailureAsync(failure);

        WriteSection("Get By ID Performance Test");

        // Act: Measure retrieval performance
        var stopwatch = Stopwatch.StartNew();
        var retrieved = await _repository.GetByIdAsync(saved.Id);
        stopwatch.Stop();

        // Assert: Verify retrieval completes within 50ms
        Assert.NotNull(retrieved);
        Assert.True(stopwatch.ElapsedMilliseconds < 50,
            $"Get by ID took {stopwatch.ElapsedMilliseconds}ms, expected < 50ms");

        Output.WriteLine($"Retrieval completed in {stopwatch.ElapsedMilliseconds}ms");
        Output.WriteLine($"Performance: {(stopwatch.ElapsedMilliseconds < 20 ? "Excellent" : "Good")}");
    }

    [Fact]
    public async Task GetHistory_Performance_WithLargeDataset_CompletesQuickly()
    {
        // Arrange: Create 1000 failure records
        WriteSection("Get History Performance Test (1000 records)");
        Output.WriteLine("Creating 1000 failure records...");

        var createStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            var failure = new FailureEntity
            {
                LogContent = $"Error {i}",
                RootCause = $"Cause {i}",
                DetailedAnalysis = $"Analysis {i}",
                ErrorType = $"Type{i % 10}",
                ConfidenceScore = 0.5 + (i % 50) * 0.01,
                AnalyzedAt = DateTime.UtcNow.AddMinutes(-i),
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 100 + i,
                OutputTokens = 50 + i
            };
            await _repository.SaveFailureAsync(failure);
        }
        createStopwatch.Stop();
        Output.WriteLine($"Created 1000 records in {createStopwatch.ElapsedMilliseconds}ms");

        // Act: Measure query performance on large dataset
        var queryStopwatch = Stopwatch.StartNew();
        var history = await _repository.GetHistoryAsync(limit: 50, offset: 0);
        queryStopwatch.Stop();

        // Assert: Verify query completes within 200ms on large dataset
        Assert.Equal(50, history.Count);
        Assert.True(queryStopwatch.ElapsedMilliseconds < 200,
            $"Query took {queryStopwatch.ElapsedMilliseconds}ms, expected < 200ms");

        Output.WriteLine($"Query (limit 50) completed in {queryStopwatch.ElapsedMilliseconds}ms");
        Output.WriteLine($"Performance: {(queryStopwatch.ElapsedMilliseconds < 100 ? "Excellent" : "Good")}");
    }

    [Fact]
    public async Task GetHistory_Performance_WithFiltering_MaintainsPerformance()
    {
        // Arrange: Create mixed error types
        WriteSection("Get History with Filtering Performance Test");
        Output.WriteLine("Creating 500 records with mixed error types...");

        for (int i = 0; i < 500; i++)
        {
            var errorType = i % 5 == 0 ? "NullReferenceException" : $"Type{i % 4}";
            var failure = new FailureEntity
            {
                LogContent = $"Error {i}",
                RootCause = $"Cause {i}",
                DetailedAnalysis = $"Analysis {i}",
                ErrorType = errorType,
                ConfidenceScore = 0.8,
                AnalyzedAt = DateTime.UtcNow.AddMinutes(-i),
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 100,
                OutputTokens = 50
            };
            await _repository.SaveFailureAsync(failure);
        }
        Output.WriteLine("Created 500 records (100 NullReferenceException)");

        // Act: Measure filtered query performance
        var stopwatch = Stopwatch.StartNew();
        var history = await _repository.GetHistoryAsync(
            limit: 50,
            errorType: "NullReferenceException");
        stopwatch.Stop();

        // Assert: Verify filtered query completes within 150ms
        Assert.Equal(50, history.Count);
        Assert.All(history, h => Assert.Equal("NullReferenceException", h.ErrorType));
        Assert.True(stopwatch.ElapsedMilliseconds < 150,
            $"Filtered query took {stopwatch.ElapsedMilliseconds}ms, expected < 150ms");

        Output.WriteLine($"Filtered query completed in {stopwatch.ElapsedMilliseconds}ms");
        Output.WriteLine($"Performance: {(stopwatch.ElapsedMilliseconds < 80 ? "Excellent" : "Good")}");
    }

    [Fact]
    public async Task GetStatistics_Performance_WithLargeDataset_CompletesQuickly()
    {
        // Arrange: Create 500 failure records
        WriteSection("Get Statistics Performance Test (500 records)");
        Output.WriteLine("Creating 500 failure records...");

        for (int i = 0; i < 500; i++)
        {
            var failure = new FailureEntity
            {
                LogContent = $"Error {i}",
                RootCause = $"Cause {i}",
                DetailedAnalysis = $"Analysis {i}",
                ErrorType = $"Type{i % 10}",
                ConfidenceScore = 0.5 + (i % 50) * 0.01,
                AnalyzedAt = DateTime.UtcNow.AddMinutes(-i),
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 100 + i,
                OutputTokens = 50 + i
            };
            await _repository.SaveFailureAsync(failure);
        }
        Output.WriteLine("Created 500 records");

        // Act: Measure statistics calculation performance
        var stopwatch = Stopwatch.StartNew();
        var stats = await _repository.GetStatisticsAsync();
        stopwatch.Stop();

        // Assert: Verify statistics calculation completes within 300ms
        Assert.Equal(500, stats.TotalFailures);
        Assert.True(stopwatch.ElapsedMilliseconds < 300,
            $"Statistics calculation took {stopwatch.ElapsedMilliseconds}ms, expected < 300ms");

        Output.WriteLine($"Statistics calculated in {stopwatch.ElapsedMilliseconds}ms");
        Output.WriteLine($"Total Failures: {stats.TotalFailures}");
        Output.WriteLine($"Avg Confidence: {stats.AverageConfidence:P0}");
        Output.WriteLine($"Total Tokens: {stats.TotalInputTokens + stats.TotalOutputTokens}");
        Output.WriteLine($"Performance: {(stopwatch.ElapsedMilliseconds < 150 ? "Excellent" : "Good")}");
    }

    [Fact]
    public async Task SaveFailureWithCommits_Performance_HandlesMultipleCommits()
    {
        // Arrange: Create failure with 20 commits
        WriteSection("Save Failure with Commits Performance Test");

        var failure = new FailureEntity
        {
            LogContent = "Error with multiple commits",
            RootCause = "Multiple changes",
            DetailedAnalysis = "Multiple commits suspected",
            ErrorType = "IntegrationError",
            ConfidenceScore = 0.85,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 500,
            OutputTokens = 300
        };

        var commits = new List<CommitEntity>();
        for (int i = 0; i < 20; i++)
        {
            commits.Add(new CommitEntity
            {
                Sha = $"commit{i}abc123",
                Author = $"Dev{i % 3}",
                CommitDate = DateTime.UtcNow.AddHours(-i),
                Message = $"Commit {i}",
                FilesChangedJson = $"[\"File{i}.cs\"]",
                ConfidenceScore = 0.7,
                SuspectedReason = "Related change"
            });
        }

        // Act: Measure save performance with commits
        var stopwatch = Stopwatch.StartNew();
        var saved = await _repository.SaveFailureWithCommitsAsync(failure, commits);
        stopwatch.Stop();

        // Assert: Verify save with 20 commits completes within 200ms
        Assert.NotEqual(0, saved.Id);
        Assert.True(stopwatch.ElapsedMilliseconds < 200,
            $"Save with 20 commits took {stopwatch.ElapsedMilliseconds}ms, expected < 200ms");

        Output.WriteLine($"Saved failure with 20 commits in {stopwatch.ElapsedMilliseconds}ms");
        Output.WriteLine($"Performance: {(stopwatch.ElapsedMilliseconds < 100 ? "Excellent" : "Good")}");
    }

    [Fact]
    public async Task GetById_Performance_WithManyCommits_MaintainsPerformance()
    {
        // Arrange: Create failure with 30 commits
        WriteSection("Get By ID with Commits Performance Test");

        var failure = new FailureEntity
        {
            LogContent = "Test error",
            RootCause = "Test cause",
            DetailedAnalysis = "Test analysis",
            ErrorType = "TestType",
            ConfidenceScore = 0.9,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 300,
            OutputTokens = 200
        };

        var commits = new List<CommitEntity>();
        for (int i = 0; i < 30; i++)
        {
            commits.Add(new CommitEntity
            {
                Sha = $"sha{i}abc",
                Author = "TestAuthor",
                CommitDate = DateTime.UtcNow.AddHours(-i),
                Message = $"Message {i}",
                FilesChangedJson = "[]",
                ConfidenceScore = 0.75,
                SuspectedReason = "Test"
            });
        }

        var saved = await _repository.SaveFailureWithCommitsAsync(failure, commits);

        // Act: Measure retrieval performance with commits
        var stopwatch = Stopwatch.StartNew();
        var retrieved = await _repository.GetByIdAsync(saved.Id, includeCommits: true);
        stopwatch.Stop();

        // Assert: Verify retrieval with 30 commits completes within 100ms
        Assert.NotNull(retrieved);
        Assert.Equal(30, retrieved.Commits.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"Get by ID with 30 commits took {stopwatch.ElapsedMilliseconds}ms, expected < 100ms");

        Output.WriteLine($"Retrieved failure with 30 commits in {stopwatch.ElapsedMilliseconds}ms");
        Output.WriteLine($"Performance: {(stopwatch.ElapsedMilliseconds < 50 ? "Excellent" : "Good")}");
    }

    [Fact]
    public async Task BulkOperations_Performance_MultipleSequentialSaves()
    {
        // Arrange: Prepare 100 failure entities
        WriteSection("Bulk Operations Performance Test (100 sequential saves)");

        var failures = new List<FailureEntity>();
        for (int i = 0; i < 100; i++)
        {
            failures.Add(new FailureEntity
            {
                LogContent = $"Error {i}",
                RootCause = $"Cause {i}",
                DetailedAnalysis = $"Analysis {i}",
                ErrorType = "BulkTest",
                ConfidenceScore = 0.8,
                AnalyzedAt = DateTime.UtcNow.AddMinutes(-i),
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 100,
                OutputTokens = 50
            });
        }

        // Act: Measure bulk save performance
        var stopwatch = Stopwatch.StartNew();
        foreach (var failure in failures)
        {
            await _repository.SaveFailureAsync(failure);
        }
        stopwatch.Stop();

        // Assert: Verify 100 sequential saves complete within 2 seconds
        Assert.True(stopwatch.ElapsedMilliseconds < 2000,
            $"100 saves took {stopwatch.ElapsedMilliseconds}ms, expected < 2000ms");

        Output.WriteLine($"100 sequential saves completed in {stopwatch.ElapsedMilliseconds}ms");
        Output.WriteLine($"Average per save: {stopwatch.ElapsedMilliseconds / 100.0:F2}ms");
        Output.WriteLine($"Performance: {(stopwatch.ElapsedMilliseconds < 1000 ? "Excellent" : "Good")}");
    }

    [Fact]
    public async Task Pagination_Performance_MultiplePageRequests()
    {
        // Arrange: Create 200 records
        WriteSection("Pagination Performance Test");
        Output.WriteLine("Creating 200 records...");

        for (int i = 0; i < 200; i++)
        {
            var failure = new FailureEntity
            {
                LogContent = $"Error {i}",
                RootCause = $"Cause {i}",
                DetailedAnalysis = $"Analysis {i}",
                ErrorType = "PageTest",
                ConfidenceScore = 0.8,
                AnalyzedAt = DateTime.UtcNow.AddMinutes(-i),
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 100,
                OutputTokens = 50
            };
            await _repository.SaveFailureAsync(failure);
        }

        // Act: Measure performance of retrieving multiple pages
        var stopwatch = Stopwatch.StartNew();
        for (int page = 0; page < 4; page++)
        {
            var history = await _repository.GetHistoryAsync(limit: 50, offset: page * 50);
            Assert.Equal(50, history.Count);
        }
        stopwatch.Stop();

        // Assert: Verify 4 page requests complete within 400ms
        Assert.True(stopwatch.ElapsedMilliseconds < 400,
            $"4 page requests took {stopwatch.ElapsedMilliseconds}ms, expected < 400ms");

        Output.WriteLine($"4 page requests completed in {stopwatch.ElapsedMilliseconds}ms");
        Output.WriteLine($"Average per page: {stopwatch.ElapsedMilliseconds / 4.0:F2}ms");
        Output.WriteLine($"Performance: {(stopwatch.ElapsedMilliseconds < 200 ? "Excellent" : "Good")}");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
