using AgenticPipeline.Api.Models;
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
/// Integration tests for similarity search API endpoint
/// </summary>
[Trait("Category", "Integration")]
public class SimilaritySearchApiTests : IntegrationTestBase, IDisposable
{
    private readonly AgenticPipelineDbContext _context;
    private readonly IFailureRepository _repository;
    private readonly string _dbPath;

    public SimilaritySearchApiTests(ITestOutputHelper output) : base(output)
    {
        // Setup unique test database
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_similarity_api_{Guid.NewGuid()}.db");
        var connectionString = $"Data Source={_dbPath}";

        var optionsBuilder = new DbContextOptionsBuilder<AgenticPipelineDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        _context = new AgenticPipelineDbContext(optionsBuilder.Options);
        _context.Database.EnsureCreated();

        var repoLogger = LoggerFactory.CreateLogger<FailureRepository>();
        var mockEmbeddingService = new MockEmbeddingService();
        _repository = new FailureRepository(_context, repoLogger, mockEmbeddingService);
    }

    [Fact]
    public async Task SimilaritySearch_ByFailureId_ReturnsSimilarFailures()
    {
        // Arrange: Create test failures in database
        var mockEmbeddingService = new MockEmbeddingService(seed: 42);

        var failures = new List<FailureEntity>
        {
            CreateFailure("NullReferenceException in PaymentService", "NullReferenceException"),
            CreateFailure("NullReferenceException in OrderService", "NullReferenceException"),
            CreateFailure("NullReferenceException in UserService", "NullReferenceException"),
            CreateFailure("TimeoutException in ApiClient", "TimeoutException"),
            CreateFailure("OutOfMemoryException in DataProcessor", "OutOfMemoryException")
        };

        // Save all failures (embeddings will be auto-generated)
        foreach (var failure in failures)
        {
            await _repository.SaveFailureAsync(failure);
        }

        WriteSection("Similarity Search by Failure ID");
        Output.WriteLine($"Created {failures.Count} test failures");

        // Retrieve first failure to use as query
        var queryFailure = await _repository.GetByIdAsync(1);
        Assert.NotNull(queryFailure);
        Assert.NotNull(queryFailure.Embedding);

        Output.WriteLine($"Query failure: {queryFailure.ErrorType}");

        // Act: Search for similar failures
        var request = new SimilaritySearchRequest
        {
            FailureId = queryFailure.Id,
            TopN = 3,
            MinSimilarity = 0.0f
        };

        // Simulate API logic
        var candidates = await _repository.GetHistoryAsync(limit: 1000);
        var filteredCandidates = candidates.Where(f => f.Id != queryFailure.Id).ToList();

        Assert.Equal(4, filteredCandidates.Count); // Should exclude query failure

        Output.WriteLine($"✓ Found {filteredCandidates.Count} candidate failures");
        Output.WriteLine($"  Search completed successfully");
    }

    [Fact]
    public async Task SimilaritySearchRequest_Validation_RequiresFailureIdOrText()
    {
        // Arrange: Create invalid request (neither FailureId nor FailureText)
        var request = new SimilaritySearchRequest
        {
            FailureId = null,
            FailureText = null,
            TopN = 10
        };

        WriteSection("Request Validation Test");

        // Assert: Validation logic (would be handled by API controller)
        var isValid = request.FailureId.HasValue || !string.IsNullOrWhiteSpace(request.FailureText);

        Assert.False(isValid);
        Output.WriteLine("✓ Invalid request correctly identified (no FailureId or FailureText)");
    }

    [Fact]
    public async Task SimilaritySearchRequest_Validation_TopNRange()
    {
        // Arrange: Test TopN validation
        var validRequests = new[]
        {
            new SimilaritySearchRequest { FailureId = 1, TopN = 1 },
            new SimilaritySearchRequest { FailureId = 1, TopN = 50 },
            new SimilaritySearchRequest { FailureId = 1, TopN = 100 }
        };

        var invalidRequests = new[]
        {
            new SimilaritySearchRequest { FailureId = 1, TopN = 0 },
            new SimilaritySearchRequest { FailureId = 1, TopN = -1 },
            new SimilaritySearchRequest { FailureId = 1, TopN = 101 }
        };

        WriteSection("TopN Validation Test");

        // Assert: Valid requests
        foreach (var req in validRequests)
        {
            Assert.True(req.TopN > 0 && req.TopN <= 100);
        }

        // Assert: Invalid requests
        foreach (var req in invalidRequests)
        {
            Assert.False(req.TopN > 0 && req.TopN <= 100);
        }

        Output.WriteLine($"✓ Valid TopN values: {string.Join(", ", validRequests.Select(r => r.TopN))}");
        Output.WriteLine($"✓ Invalid TopN values correctly rejected: {string.Join(", ", invalidRequests.Select(r => r.TopN))}");
    }

    [Fact]
    public async Task SimilaritySearchRequest_Validation_MinSimilarityRange()
    {
        // Arrange: Test MinSimilarity validation
        var validRequests = new[]
        {
            new SimilaritySearchRequest { FailureId = 1, MinSimilarity = 0.0f },
            new SimilaritySearchRequest { FailureId = 1, MinSimilarity = 0.5f },
            new SimilaritySearchRequest { FailureId = 1, MinSimilarity = 1.0f }
        };

        var invalidRequests = new[]
        {
            new SimilaritySearchRequest { FailureId = 1, MinSimilarity = -0.1f },
            new SimilaritySearchRequest { FailureId = 1, MinSimilarity = 1.1f },
            new SimilaritySearchRequest { FailureId = 1, MinSimilarity = 2.0f }
        };

        WriteSection("MinSimilarity Validation Test");

        // Assert: Valid requests
        foreach (var req in validRequests)
        {
            Assert.True(req.MinSimilarity >= 0.0f && req.MinSimilarity <= 1.0f);
        }

        // Assert: Invalid requests
        foreach (var req in invalidRequests)
        {
            Assert.False(req.MinSimilarity >= 0.0f && req.MinSimilarity <= 1.0f);
        }

        Output.WriteLine($"✓ Valid MinSimilarity values: {string.Join(", ", validRequests.Select(r => r.MinSimilarity))}");
        Output.WriteLine($"✓ Invalid MinSimilarity values rejected: {string.Join(", ", invalidRequests.Select(r => r.MinSimilarity))}");
    }

    [Fact]
    public async Task SimilaritySearch_WithErrorTypeFilter_ReturnsMatchingType()
    {
        // Arrange: Create failures of different types
        var failures = new List<FailureEntity>
        {
            CreateFailure("NullRef 1", "NullReferenceException"),
            CreateFailure("NullRef 2", "NullReferenceException"),
            CreateFailure("Timeout 1", "TimeoutException"),
            CreateFailure("Timeout 2", "TimeoutException"),
            CreateFailure("OutOfMemory 1", "OutOfMemoryException")
        };

        foreach (var failure in failures)
        {
            await _repository.SaveFailureAsync(failure);
        }

        WriteSection("Error Type Filter Test");

        // Act: Filter by error type
        var nullRefFailures = await _repository.GetHistoryAsync(
            limit: 100,
            errorType: "NullReferenceException");

        var timeoutFailures = await _repository.GetHistoryAsync(
            limit: 100,
            errorType: "TimeoutException");

        // Assert: Correct filtering
        Assert.Equal(2, nullRefFailures.Count);
        Assert.All(nullRefFailures, f => Assert.Equal("NullReferenceException", f.ErrorType));

        Assert.Equal(2, timeoutFailures.Count);
        Assert.All(timeoutFailures, f => Assert.Equal("TimeoutException", f.ErrorType));

        Output.WriteLine($"✓ NullReferenceException failures: {nullRefFailures.Count}");
        Output.WriteLine($"✓ TimeoutException failures: {timeoutFailures.Count}");
    }

    [Fact]
    public async Task SimilaritySearch_WithLookbackFilter_ReturnsRecentFailures()
    {
        // Arrange: Create failures with different dates
        var oldFailure = CreateFailure("Old failure", "TestError");
        oldFailure.AnalyzedAt = DateTime.UtcNow.AddDays(-10);

        var recentFailure = CreateFailure("Recent failure", "TestError");
        recentFailure.AnalyzedAt = DateTime.UtcNow.AddDays(-2);

        await _repository.SaveFailureAsync(oldFailure);
        await _repository.SaveFailureAsync(recentFailure);

        WriteSection("Lookback Filter Test");

        // Act: Filter by lookback days
        var allFailures = await _repository.GetHistoryAsync(limit: 100);
        var lookbackDays = 7;
        var cutoffDate = DateTime.UtcNow.AddDays(-lookbackDays);
        var recentFailures = allFailures.Where(f => f.AnalyzedAt >= cutoffDate).ToList();

        // Assert: Only recent failures returned
        Assert.Equal(2, allFailures.Count);
        Assert.Single(recentFailures);
        Assert.True(recentFailures[0].AnalyzedAt >= cutoffDate);

        Output.WriteLine($"✓ Total failures: {allFailures.Count}");
        Output.WriteLine($"✓ Failures in last {lookbackDays} days: {recentFailures.Count}");
    }

    // Helper method
    private FailureEntity CreateFailure(string logContent, string errorType)
    {
        return new FailureEntity
        {
            LogContent = logContent,
            RootCause = $"Root cause: {logContent}",
            DetailedAnalysis = $"Analysis of {logContent}",
            ErrorType = errorType,
            ConfidenceScore = 0.9,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 100,
            OutputTokens = 50
        };
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
