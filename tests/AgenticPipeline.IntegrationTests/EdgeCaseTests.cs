using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Data;
using AgenticPipeline.Infrastructure.Repositories;
using AgenticPipeline.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace AgenticPipeline.IntegrationTests;

/// <summary>
/// Edge case and boundary testing for the pipeline system
/// </summary>
public class EdgeCaseTests : IntegrationTestBase, IDisposable
{
    private readonly AgenticPipelineDbContext _context;
    private readonly IFailureRepository _repository;

    public EdgeCaseTests(ITestOutputHelper output) : base(output)
    {
        // Arrange: Setup unique test database
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_edgecase_{Guid.NewGuid()}.db");
        var connectionString = $"Data Source={dbPath}";

        var optionsBuilder = new DbContextOptionsBuilder<AgenticPipelineDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        _context = new AgenticPipelineDbContext(optionsBuilder.Options);
        _context.Database.EnsureCreated();

        var repoLogger = LoggerFactory.CreateLogger<FailureRepository>();
        _repository = new FailureRepository(_context, repoLogger);
    }

    [Fact]
    public async Task SaveFailure_WithVeryLongLogContent_SavesSuccessfully()
    {
        // Arrange: Create log content larger than 10KB
        var largeLogBuilder = new StringBuilder();
        largeLogBuilder.AppendLine("ERROR: Application crash with extensive stack trace");

        for (int i = 0; i < 500; i++)
        {
            largeLogBuilder.AppendLine($"  at Module{i}.Method{i}(param1, param2, param3) in File{i}.cs:line {i * 10}");
        }

        var largeLog = largeLogBuilder.ToString();
        Output.WriteLine($"Log content size: {largeLog.Length} characters ({largeLog.Length / 1024.0:F2} KB)");

        var failure = new FailureEntity
        {
            LogContent = largeLog,
            RootCause = "Stack overflow in recursive method",
            DetailedAnalysis = "Deep recursion caused stack overflow",
            ErrorType = "StackOverflowException",
            ConfidenceScore = 0.92,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[\"RecursiveModule\"]",
            RecommendationsJson = "[\"Add base case\", \"Limit recursion depth\"]",
            InputTokens = 5000,
            OutputTokens = 800
        };

        // Act: Save large failure entity
        var saved = await _repository.SaveFailureAsync(failure);

        // Assert: Verify large content was saved and can be retrieved
        Assert.NotEqual(0, saved.Id);
        var retrieved = await _repository.GetByIdAsync(saved.Id, includeCommits: false);
        Assert.NotNull(retrieved);
        Assert.Equal(largeLog.Length, retrieved.LogContent.Length);
        Output.WriteLine($"Successfully saved and retrieved large log content");
    }

    [Fact]
    public async Task SaveFailure_WithManyCommits_SavesAllCommits()
    {
        // Arrange: Create failure with 50 suspected commits
        var failure = new FailureEntity
        {
            LogContent = "Error with many potential causes",
            RootCause = "Multiple changes in short timeframe",
            DetailedAnalysis = "Many commits made before failure",
            ErrorType = "IntegrationError",
            ConfidenceScore = 0.75,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 2000,
            OutputTokens = 500
        };

        var commits = new List<CommitEntity>();
        for (int i = 0; i < 50; i++)
        {
            commits.Add(new CommitEntity
            {
                Sha = $"commit{i:D3}abc123def456",
                Author = $"Developer{i % 5}",
                CommitDate = DateTime.UtcNow.AddHours(-i),
                Message = $"Commit {i}: Updated feature module",
                FilesChangedJson = $"[\"Module{i}.cs\", \"Test{i}.cs\"]",
                ConfidenceScore = 0.5 + (i * 0.005),
                SuspectedReason = $"Modified related files"
            });
        }

        Output.WriteLine($"Saving failure with {commits.Count} commits");

        // Act: Save failure with many commits
        var saved = await _repository.SaveFailureWithCommitsAsync(failure, commits);

        // Assert: Verify all commits were saved
        Assert.NotEqual(0, saved.Id);
        var retrieved = await _repository.GetByIdAsync(saved.Id, includeCommits: true);
        Assert.NotNull(retrieved);
        Assert.Equal(50, retrieved.Commits.Count);
        Output.WriteLine($"Successfully saved and retrieved {retrieved.Commits.Count} commits");
    }

    [Fact]
    public async Task GetHistory_WithMaxLimit_ReturnsCorrectCount()
    {
        // Arrange: Create 150 failures in database
        Output.WriteLine("Creating 150 failure records...");
        for (int i = 0; i < 150; i++)
        {
            var failure = new FailureEntity
            {
                LogContent = $"Error {i}",
                RootCause = $"Cause {i}",
                DetailedAnalysis = $"Analysis {i}",
                ErrorType = "TestError",
                ConfidenceScore = 0.8,
                AnalyzedAt = DateTime.UtcNow.AddMinutes(-i),
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 100,
                OutputTokens = 50
            };
            await _repository.SaveFailureAsync(failure);
        }
        Output.WriteLine("Created 150 failure records");

        // Act: Request maximum allowed limit (100)
        var history = await _repository.GetHistoryAsync(limit: 100);

        // Assert: Verify exactly 100 records returned
        Assert.Equal(100, history.Count);
        Output.WriteLine($"Max limit correctly enforced: returned {history.Count} records");
    }

    [Fact]
    public async Task SaveFailure_WithSpecialCharactersInJson_HandlesCorrectly()
    {
        // Arrange: Create failure with special characters in JSON fields
        var failure = new FailureEntity
        {
            LogContent = "Error with quotes and special chars: \"test\" & <xml> 'single'",
            RootCause = "Parser failed on special characters: \", ', <, >, &",
            DetailedAnalysis = "Special chars: \n\t\r\\\" caused parsing issues",
            ErrorType = "ParseException",
            ConfidenceScore = 0.88,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[\"Parser\\\"Module\", \"XML<Handler>\"]",
            RecommendationsJson = "[\"Escape special chars\", \"Use proper encoding\"]",
            InputTokens = 300,
            OutputTokens = 150
        };

        // Act: Save and retrieve failure with special characters
        var saved = await _repository.SaveFailureAsync(failure);
        var retrieved = await _repository.GetByIdAsync(saved.Id, includeCommits: false);

        // Assert: Verify special characters preserved correctly
        Assert.NotNull(retrieved);
        Assert.Contains("\"test\"", retrieved.LogContent);
        Assert.Contains("<xml>", retrieved.LogContent);
        Assert.Contains("Parser\\\"Module", retrieved.AffectedComponentsJson);
        Output.WriteLine("Special characters handled correctly in JSON fields");
    }

    [Fact]
    public async Task SaveFailure_WithVeryHighConfidence_SavesCorrectly()
    {
        // Arrange: Create failure with edge case confidence values
        var failure1 = new FailureEntity
        {
            LogContent = "Error 1",
            RootCause = "Cause 1",
            DetailedAnalysis = "Analysis 1",
            ErrorType = "Type1",
            ConfidenceScore = 1.0, // Maximum
            AnalyzedAt = DateTime.UtcNow,
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
            ConfidenceScore = 0.0, // Minimum
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 100,
            OutputTokens = 50
        };

        // Act: Save failures with edge case confidence values
        var saved1 = await _repository.SaveFailureAsync(failure1);
        var saved2 = await _repository.SaveFailureAsync(failure2);

        // Assert: Verify edge case confidence values preserved
        Assert.Equal(1.0, saved1.ConfidenceScore);
        Assert.Equal(0.0, saved2.ConfidenceScore);
        Output.WriteLine("Edge case confidence values (0.0 and 1.0) handled correctly");
    }

    [Fact]
    public async Task GetHistory_WithLargeOffset_ReturnsCorrectResults()
    {
        // Arrange: Create 100 failures
        Output.WriteLine("Creating 100 failure records for offset testing...");
        for (int i = 0; i < 100; i++)
        {
            var failure = new FailureEntity
            {
                LogContent = $"Error {i}",
                RootCause = $"Cause {i}",
                DetailedAnalysis = $"Analysis {i}",
                ErrorType = "TestError",
                ConfidenceScore = 0.8,
                AnalyzedAt = DateTime.UtcNow.AddMinutes(-i),
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 100,
                OutputTokens = 50
            };
            await _repository.SaveFailureAsync(failure);
        }

        // Act: Query with large offset (skip first 95 records)
        var history = await _repository.GetHistoryAsync(limit: 10, offset: 95);

        // Assert: Verify correct number of records returned (only 5 remain)
        Assert.Equal(5, history.Count);
        Output.WriteLine($"Large offset handled correctly: returned {history.Count} remaining records");
    }

    [Fact]
    public async Task SaveFailure_WithEmptyJsonArrays_SavesSuccessfully()
    {
        // Arrange: Create failure with empty JSON arrays
        var failure = new FailureEntity
        {
            LogContent = "Error with no affected components",
            RootCause = "Unknown cause",
            DetailedAnalysis = "Unable to identify components",
            ErrorType = "UnknownError",
            ConfidenceScore = 0.3,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]", // Empty array
            RecommendationsJson = "[]",     // Empty array
            InputTokens = 100,
            OutputTokens = 50
        };

        // Act: Save failure with empty JSON arrays
        var saved = await _repository.SaveFailureAsync(failure);

        // Assert: Verify empty arrays preserved correctly
        Assert.NotEqual(0, saved.Id);
        var retrieved = await _repository.GetByIdAsync(saved.Id, includeCommits: false);
        Assert.NotNull(retrieved);
        Assert.Equal("[]", retrieved.AffectedComponentsJson);
        Assert.Equal("[]", retrieved.RecommendationsJson);
        Output.WriteLine("Empty JSON arrays handled correctly");
    }

    [Fact]
    public async Task GetStatistics_WithSingleFailure_CalculatesCorrectly()
    {
        // Arrange: Create exactly one failure
        var failure = new FailureEntity
        {
            LogContent = "Single error",
            RootCause = "Single cause",
            DetailedAnalysis = "Single analysis",
            ErrorType = "SingleType",
            ConfidenceScore = 0.95,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 200,
            OutputTokens = 100
        };
        await _repository.SaveFailureAsync(failure);

        // Act: Calculate statistics for single record
        var stats = await _repository.GetStatisticsAsync();

        // Assert: Verify statistics calculated correctly for single record
        Assert.Equal(1, stats.TotalFailures);
        Assert.Equal(0.95, stats.AverageConfidence);
        Assert.Equal(200, stats.TotalInputTokens);
        Assert.Equal(100, stats.TotalOutputTokens);
        Assert.Single(stats.FailuresByErrorType);
        Assert.Equal(1, stats.FailuresByErrorType["SingleType"]);
        Output.WriteLine("Statistics for single record calculated correctly");
    }

    [Fact]
    public async Task SaveFailure_WithNullOptionalFields_SavesSuccessfully()
    {
        // Arrange: Create failure with minimal required fields only
        var failure = new FailureEntity
        {
            LogContent = "Minimal error",
            RootCause = "Minimal cause",
            DetailedAnalysis = "Minimal analysis",
            ErrorType = "MinimalError",
            ConfidenceScore = 0.8,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 100,
            OutputTokens = 50,
            // Optional fields left null/empty
            SuspectedCommitsJson = null,
            Repository = null,
            CommitsAnalyzed = null,
            Embedding = null
        };

        // Act: Save minimal failure entity
        var saved = await _repository.SaveFailureAsync(failure);

        // Assert: Verify entity saved successfully with null optional fields
        Assert.NotEqual(0, saved.Id);
        var retrieved = await _repository.GetByIdAsync(saved.Id, includeCommits: false);
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.SuspectedCommitsJson);
        Assert.Null(retrieved.Repository);
        Assert.Null(retrieved.CommitsAnalyzed);
        Assert.Null(retrieved.Embedding);
        Output.WriteLine("Minimal entity with null optional fields saved correctly");
    }

    [Fact]
    public async Task GetHistory_WithNonExistentErrorType_ReturnsEmptyList()
    {
        // Arrange: Create failures with known error types
        var failure = new FailureEntity
        {
            LogContent = "Error",
            RootCause = "Cause",
            DetailedAnalysis = "Analysis",
            ErrorType = "KnownType",
            ConfidenceScore = 0.8,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 100,
            OutputTokens = 50
        };
        await _repository.SaveFailureAsync(failure);

        // Act: Query with non-existent error type
        var history = await _repository.GetHistoryAsync(errorType: "NonExistentType");

        // Assert: Verify empty list returned (not null or exception)
        Assert.NotNull(history);
        Assert.Empty(history);
        Output.WriteLine("Non-existent error type filter handled correctly (returns empty list)");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
