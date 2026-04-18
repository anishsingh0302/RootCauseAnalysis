using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Data;
using AgenticPipeline.Infrastructure.Extensions;
using AgenticPipeline.Infrastructure.Plugins;
using AgenticPipeline.Infrastructure.Repositories;
using AgenticPipeline.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AgenticPipeline.IntegrationTests;

/// <summary>
/// Integration tests for API endpoints with database persistence
/// </summary>
public class ApiIntegrationTests : IntegrationTestBase, IDisposable
{
    private readonly AgenticPipelineDbContext _context;
    private readonly RcaPlugin _rcaPlugin;
    private readonly IFailureRepository _repository;
    private readonly string _testLogContent;

    public ApiIntegrationTests(ITestOutputHelper output) : base(output)
    {
        // Setup unique test database
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_api_{Guid.NewGuid()}.db");
        var connectionString = $"Data Source={dbPath}";

        var optionsBuilder = new DbContextOptionsBuilder<AgenticPipelineDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        _context = new AgenticPipelineDbContext(optionsBuilder.Options);
        _context.Database.EnsureCreated();

        var repoLogger = LoggerFactory.CreateLogger<FailureRepository>();
        _repository = new FailureRepository(_context, repoLogger);

        // Setup RCA plugin
        var factory = new TestServiceFactory(LoggerFactory);
        _rcaPlugin = factory.CreateRcaPlugin();

        _testLogContent = @"
ERROR: Build failed
NullReferenceException at PaymentService.ProcessPayment()
Stack trace: Object reference not set to an instance of an object
";
    }

    [Fact]
    public async Task AnalyzePipeline_WithValidLog_SavesResultToDatabase()
    {
        // Arrange: Prepare log content and perform analysis
        var resultJson = await _rcaPlugin.AnalyzeFailureAsync(_testLogContent, CancellationToken.None);
        var result = JsonSerializer.Deserialize<RcaResult>(resultJson);
        Assert.NotNull(result);

        // Act: Save result to database (simulating controller behavior)
        var failureEntity = result.ToEntity(_testLogContent);
        var savedEntity = await _repository.SaveFailureAsync(failureEntity, CancellationToken.None);

        // Assert: Verify entity was saved with valid ID
        Assert.NotEqual(0, savedEntity.Id);
        Output.WriteLine($"Saved failure analysis with ID: {savedEntity.Id}");

        // Assert: Verify can retrieve from database
        var retrievedEntity = await _repository.GetByIdAsync(savedEntity.Id, includeCommits: false);
        Assert.NotNull(retrievedEntity);
        Assert.Equal(result.RootCause, retrievedEntity.RootCause);
        Assert.Equal(result.ErrorType, retrievedEntity.ErrorType);
        Assert.Equal(_testLogContent, retrievedEntity.LogContent);
    }

    [Fact]
    public async Task GetById_AfterSave_RetrievesCorrectData()
    {
        // Arrange: Create and save a failure entity
        var resultJson = await _rcaPlugin.AnalyzeFailureAsync(_testLogContent, CancellationToken.None);
        var result = JsonSerializer.Deserialize<RcaResult>(resultJson);
        Assert.NotNull(result);

        var failureEntity = result.ToEntity(_testLogContent);
        var savedEntity = await _repository.SaveFailureAsync(failureEntity, CancellationToken.None);

        // Act: Retrieve the saved entity by ID
        var retrievedEntity = await _repository.GetByIdAsync(savedEntity.Id);

        // Assert: Verify retrieved data matches saved data
        Assert.NotNull(retrievedEntity);
        Assert.Equal(savedEntity.Id, retrievedEntity.Id);
        Assert.Equal(savedEntity.RootCause, retrievedEntity.RootCause);
        Assert.Equal(savedEntity.DetailedAnalysis, retrievedEntity.DetailedAnalysis);
        Assert.Equal(savedEntity.ErrorType, retrievedEntity.ErrorType);
        Assert.Equal(savedEntity.ConfidenceScore, retrievedEntity.ConfidenceScore);
        Output.WriteLine($"Successfully retrieved failure {retrievedEntity.Id} from database");
    }

    [Fact]
    public async Task SaveFailureWithCommits_WithSuspectedCommits_SavesBothEntities()
    {
        // Arrange: Create failure entity with suspected commits
        var failure = new FailureEntity
        {
            LogContent = _testLogContent,
            RootCause = "NullReferenceException in payment processing",
            DetailedAnalysis = "The payment service attempted to dereference a null object",
            ErrorType = "NullReferenceException",
            ConfidenceScore = 0.95,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[\"PaymentService\"]",
            RecommendationsJson = "[\"Add null check\"]",
            InputTokens = 1000,
            OutputTokens = 500
        };

        var commits = new List<CommitEntity>
        {
            new CommitEntity
            {
                Sha = "abc123",
                Author = "John Doe",
                CommitDate = DateTime.UtcNow.AddHours(-2),
                Message = "Update payment processing logic",
                FilesChangedJson = "[\"PaymentService.cs\"]",
                ConfidenceScore = 0.85,
                SuspectedReason = "Modified payment processing code"
            }
        };

        // Act: Save failure with related commits
        var savedEntity = await _repository.SaveFailureWithCommitsAsync(failure, commits, CancellationToken.None);

        // Assert: Verify both failure and commits were saved
        Assert.NotEqual(0, savedEntity.Id);
        var retrieved = await _repository.GetByIdAsync(savedEntity.Id, includeCommits: true);
        Assert.NotNull(retrieved);
        Assert.Single(retrieved.Commits);
        Assert.Equal("abc123", retrieved.Commits.First().Sha);
        Output.WriteLine($"Saved failure with {retrieved.Commits.Count} related commits");
    }

    [Fact]
    public async Task GetHistory_WithMultipleFailures_ReturnsInDescendingOrder()
    {
        // Arrange: Create and save multiple failure entities with different timestamps
        var failure1 = new FailureEntity
        {
            LogContent = "Error 1",
            RootCause = "Cause 1",
            DetailedAnalysis = "Analysis 1",
            ErrorType = "Type1",
            ConfidenceScore = 0.8,
            AnalyzedAt = DateTime.UtcNow.AddHours(-3),
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
            AnalyzedAt = DateTime.UtcNow.AddHours(-1),
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 200,
            OutputTokens = 100
        };

        await _repository.SaveFailureAsync(failure1, CancellationToken.None);
        await _repository.SaveFailureAsync(failure2, CancellationToken.None);

        // Act: Retrieve failure history
        var history = await _repository.GetHistoryAsync(limit: 10);

        // Assert: Verify results are ordered by most recent first
        Assert.True(history.Count >= 2);
        Assert.True(history[0].AnalyzedAt >= history[1].AnalyzedAt);
        Output.WriteLine($"Retrieved {history.Count} failures, most recent: {history[0].AnalyzedAt}");
    }

    [Fact]
    public async Task ToEntity_AndBack_PreservesAllData()
    {
        // Arrange: Create RcaResult with all fields populated
        var originalResult = new RcaResult(
            RootCause: "Database connection timeout",
            DetailedAnalysis: "The application failed to connect to the database within the timeout period",
            AffectedComponents: new List<string> { "DatabaseService", "ConnectionPool" },
            ErrorType: "TimeoutException",
            ConfidenceScore: 0.92,
            Recommendations: new List<string> { "Increase timeout", "Check network" },
            TokenUsage: new TokenUsage(1500, 750),
            AnalyzedAt: DateTime.UtcNow,
            SuspectedCommits: new List<SuspectedCommit>
            {
                new SuspectedCommit(
                    Sha: "def456",
                    ShortSha: "def456",
                    Message: "Update database configuration",
                    Author: "Jane Smith",
                    CommitDate: DateTime.UtcNow.AddHours(-5),
                    Confidence: 0.88,
                    Reason: "Changed connection string",
                    SuspectedFiles: new List<string> { "appsettings.json" },
                    Evidence: "Modified timeout settings"
                )
            },
            CommitCorrelation: new CommitCorrelationMetadata(
                Repository: "myorg/myapp",
                TotalCommitsAnalyzed: 10,
                TimeRangeStart: DateTime.UtcNow.AddHours(-24),
                TimeRangeEnd: DateTime.UtcNow,
                LookbackHours: 24
            )
        );

        // Act: Convert to entity and back to RcaResult
        var entity = originalResult.ToEntity("test log content");
        var roundTripResult = entity.ToRcaResult();

        // Assert: Verify all important data was preserved
        Assert.Equal(originalResult.RootCause, roundTripResult.RootCause);
        Assert.Equal(originalResult.DetailedAnalysis, roundTripResult.DetailedAnalysis);
        Assert.Equal(originalResult.ErrorType, roundTripResult.ErrorType);
        Assert.Equal(originalResult.ConfidenceScore, roundTripResult.ConfidenceScore);
        Assert.Equal(originalResult.AffectedComponents.Count, roundTripResult.AffectedComponents.Count);
        Assert.Equal(originalResult.Recommendations.Count, roundTripResult.Recommendations.Count);
        Assert.Equal(originalResult.SuspectedCommits?.Count, roundTripResult.SuspectedCommits?.Count);
        Assert.NotNull(roundTripResult.CommitCorrelation);
        Assert.Equal("myorg/myapp", roundTripResult.CommitCorrelation.Repository);
        Output.WriteLine("Round-trip conversion preserved all data correctly");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
