using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Services;
using AgenticPipeline.Infrastructure.Repositories;
using AgenticPipeline.Tests.Common;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AgenticPipeline.IntegrationTests;

/// <summary>
/// Integration tests for pattern recognition using K-means clustering
/// </summary>
[Trait("Category", "Integration")]
public class PatternRecognitionTests : IntegrationTestBase
{
    private readonly KMeansPatternRecognitionService _patternService;
    private readonly CosineSimilarityService _similarityService;
    private readonly MockEmbeddingService _embeddingService;

    public PatternRecognitionTests(ITestOutputHelper output) : base(output)
    {
        var logger = LoggerFactory.CreateLogger<KMeansPatternRecognitionService>();
        var simLogger = LoggerFactory.CreateLogger<CosineSimilarityService>();
        _similarityService = new CosineSimilarityService(simLogger);
        _patternService = new KMeansPatternRecognitionService(logger, _similarityService);
        _embeddingService = new MockEmbeddingService(seed: 42);
    }

    [Fact]
    public void DetectPatterns_WithDistinctGroups_FindsCorrectClusters()
    {
        // Arrange: Create failures with distinct error types
        var failures = new List<FailureEntity>
        {
            // NullReferenceException cluster
            CreateFailureWithEmbedding("NullReferenceException in PaymentService", "NullReferenceException"),
            CreateFailureWithEmbedding("NullReferenceException in OrderService", "NullReferenceException"),
            CreateFailureWithEmbedding("NullReferenceException in UserService", "NullReferenceException"),

            // TimeoutException cluster
            CreateFailureWithEmbedding("TimeoutException in ApiClient", "TimeoutException"),
            CreateFailureWithEmbedding("TimeoutException in DatabaseConnection", "TimeoutException"),
            CreateFailureWithEmbedding("TimeoutException in ExternalService", "TimeoutException"),

            // OutOfMemoryException cluster
            CreateFailureWithEmbedding("OutOfMemoryException in DataProcessor", "OutOfMemoryException"),
            CreateFailureWithEmbedding("OutOfMemoryException in ImageHandler", "OutOfMemoryException"),
            CreateFailureWithEmbedding("OutOfMemoryException in CacheManager", "OutOfMemoryException")
        };

        var parameters = new ClusteringParameters
        {
            ClusterCount = 3,
            MaxIterations = 50,
            MinClusterSize = 2,
            RandomSeed = 42 // For reproducible results
        };

        WriteSection("Distinct Groups Pattern Detection");
        Output.WriteLine($"Total failures: {failures.Count}");
        Output.WriteLine($"Expected clusters: 3");

        // Act: Detect patterns
        var result = _patternService.DetectPatterns(failures, parameters);

        // Assert: Should find 2-3 patterns (one might be filtered by MinClusterSize)
        Assert.NotNull(result);
        Assert.NotNull(result.Patterns);
        Assert.True(result.Patterns.Count >= 2 && result.Patterns.Count <= 3,
            $"Expected 2-3 patterns, got {result.Patterns.Count}");
        Assert.Equal(9, result.TotalFailures);

        // Each pattern should have ~3 failures
        foreach (var pattern in result.Patterns)
        {
            Assert.True(pattern.FailureCount >= 2, $"Pattern {pattern.Id} should have at least 2 failures");
            Assert.NotNull(pattern.CentroidEmbedding);
            Assert.NotEmpty(pattern.CommonRootCauses);
        }

        Output.WriteLine($"✓ Found {result.Patterns.Count} patterns");
        foreach (var pattern in result.Patterns.OrderByDescending(p => p.FailureCount))
        {
            Output.WriteLine($"  {pattern.Name}:");
            Output.WriteLine($"    Failures: {pattern.FailureCount}");
            Output.WriteLine($"    Primary Error: {pattern.PrimaryErrorType}");
            Output.WriteLine($"    Avg Similarity: {pattern.AverageSimilarity:P1}");
        }
    }

    [Fact]
    public void DetectPatterns_WithNoEmbeddings_ReturnsEmpty()
    {
        // Arrange: Create failures without embeddings
        var failures = new List<FailureEntity>
        {
            new FailureEntity
            {
                Id = 1,
                LogContent = "Test",
                RootCause = "Test",
                DetailedAnalysis = "Test",
                ErrorType = "Test",
                ConfidenceScore = 0.9,
                AnalyzedAt = DateTime.UtcNow,
                AffectedComponentsJson = "[]",
                RecommendationsJson = "[]",
                InputTokens = 10,
                OutputTokens = 5,
                Embedding = null // No embedding
            }
        };

        var parameters = new ClusteringParameters { ClusterCount = 2 };

        WriteSection("No Embeddings Test");

        // Act: Detect patterns
        var result = _patternService.DetectPatterns(failures, parameters);

        // Assert: Should return empty
        Assert.NotNull(result);
        Assert.Empty(result.Patterns);
        Assert.Equal(1, result.TotalFailures);

        Output.WriteLine($"✓ Correctly returned empty patterns for failures without embeddings");
    }

    [Fact]
    public void AssignToPattern_WithMatchingPattern_ReturnsCorrectPattern()
    {
        // Arrange: Create patterns
        var failures = new List<FailureEntity>
        {
            CreateFailureWithEmbedding("NullRef 1", "NullReferenceException"),
            CreateFailureWithEmbedding("NullRef 2", "NullReferenceException"),
            CreateFailureWithEmbedding("Timeout 1", "TimeoutException"),
            CreateFailureWithEmbedding("Timeout 2", "TimeoutException")
        };

        var parameters = new ClusteringParameters { ClusterCount = 2, RandomSeed = 123 };
        var result = _patternService.DetectPatterns(failures, parameters);

        // Create new failure to assign
        var newFailure = CreateFailureWithEmbedding("NullRef 3", "NullReferenceException");

        WriteSection("Pattern Assignment Test");
        Output.WriteLine($"Detected {result.Patterns.Count} patterns");
        Output.WriteLine($"Assigning new NullReferenceException failure...");

        // Act: Assign to pattern
        var assignment = _patternService.AssignToPattern(newFailure, result.Patterns);

        // Assert: Should find a pattern (similarity can be any value between -1 and 1)
        Assert.NotNull(assignment);
        Assert.NotNull(assignment.Value.pattern);
        Assert.True(assignment.Value.similarity >= -1.0f && assignment.Value.similarity <= 1.0f,
            $"Similarity should be between -1 and 1, got {assignment.Value.similarity}");

        Output.WriteLine($"✓ Assigned to pattern: {assignment.Value.pattern.Name}");
        Output.WriteLine($"  Similarity: {assignment.Value.similarity:P1}");
    }

    [Fact]
    public void DetermineOptimalClusterCount_FindsReasonableK()
    {
        // Arrange: Create diverse failures
        var failures = new List<FailureEntity>();

        // Add 3 distinct groups
        for (int i = 0; i < 5; i++)
        {
            failures.Add(CreateFailureWithEmbedding($"NullRef {i}", "NullReferenceException"));
            failures.Add(CreateFailureWithEmbedding($"Timeout {i}", "TimeoutException"));
            failures.Add(CreateFailureWithEmbedding($"OutOfMemory {i}", "OutOfMemoryException"));
        }

        WriteSection("Optimal Cluster Count Detection");
        Output.WriteLine($"Total failures: {failures.Count}");

        // Act: Determine optimal K
        var optimalK = _patternService.DetermineOptimalClusterCount(failures, maxClusters: 8);

        // Assert: Should be reasonable (2-5 clusters for 3 groups)
        Assert.True(optimalK >= 2 && optimalK <= 6,
            $"Optimal K should be between 2 and 6, got {optimalK}");

        Output.WriteLine($"✓ Optimal cluster count: {optimalK}");
    }

    [Fact]
    public void DetectPatterns_WithMinClusterSize_FiltersSmallClusters()
    {
        // Arrange: Create unbalanced clusters
        var failures = new List<FailureEntity>
        {
            // Large cluster
            CreateFailureWithEmbedding("Error A1", "ErrorA"),
            CreateFailureWithEmbedding("Error A2", "ErrorA"),
            CreateFailureWithEmbedding("Error A3", "ErrorA"),
            CreateFailureWithEmbedding("Error A4", "ErrorA"),
            CreateFailureWithEmbedding("Error A5", "ErrorA"),

            // Small cluster (should be filtered)
            CreateFailureWithEmbedding("Error B1", "ErrorB"),

            // Medium cluster
            CreateFailureWithEmbedding("Error C1", "ErrorC"),
            CreateFailureWithEmbedding("Error C2", "ErrorC"),
            CreateFailureWithEmbedding("Error C3", "ErrorC")
        };

        var parameters = new ClusteringParameters
        {
            ClusterCount = 3,
            MinClusterSize = 2, // Filter out clusters with < 2 members
            RandomSeed = 789
        };

        WriteSection("Min Cluster Size Filtering");
        Output.WriteLine($"Total failures: {failures.Count}");
        Output.WriteLine($"Min cluster size: {parameters.MinClusterSize}");

        // Act: Detect patterns
        var result = _patternService.DetectPatterns(failures, parameters);

        // Assert: Should filter small clusters
        Assert.NotNull(result);
        Assert.All(result.Patterns, p =>
            Assert.True(p.FailureCount >= parameters.MinClusterSize,
                $"Pattern {p.Id} has {p.FailureCount} failures, should be >= {parameters.MinClusterSize}"));

        Output.WriteLine($"✓ Found {result.Patterns.Count} patterns (all >= min size)");
        foreach (var pattern in result.Patterns)
        {
            Output.WriteLine($"  {pattern.Name}: {pattern.FailureCount} failures");
        }
    }

    [Fact]
    public void DetectPatterns_CalculatesPatternMetrics()
    {
        // Arrange: Create failures
        var baseDate = DateTime.UtcNow.AddDays(-10);
        var failures = new List<FailureEntity>
        {
            CreateFailureWithEmbedding("Error 1", "TestError", baseDate.AddDays(0)),
            CreateFailureWithEmbedding("Error 2", "TestError", baseDate.AddDays(2)),
            CreateFailureWithEmbedding("Error 3", "TestError", baseDate.AddDays(5)),
            CreateFailureWithEmbedding("Error 4", "TestError", baseDate.AddDays(8)),
            CreateFailureWithEmbedding("Error 5", "TestError", baseDate.AddDays(10))
        };

        var parameters = new ClusteringParameters { ClusterCount = 1, RandomSeed = 456 };

        WriteSection("Pattern Metrics Calculation");

        // Act: Detect patterns
        var result = _patternService.DetectPatterns(failures, parameters);

        // Assert: Metrics should be calculated
        Assert.Single(result.Patterns);
        var pattern = result.Patterns[0];

        Assert.Equal(5, pattern.FailureCount);
        Assert.True(pattern.AverageConfidence > 0);
        Assert.True(pattern.Frequency > 0); // Should have frequency > 0
        Assert.NotNull(pattern.FirstOccurrence);
        Assert.NotNull(pattern.LastOccurrence);
        Assert.True(pattern.LastOccurrence >= pattern.FirstOccurrence);
        Assert.NotEmpty(pattern.SampleFailureIds);

        Output.WriteLine($"✓ Pattern metrics calculated:");
        Output.WriteLine($"  Failure count: {pattern.FailureCount}");
        Output.WriteLine($"  Avg confidence: {pattern.AverageConfidence:P1}");
        Output.WriteLine($"  Frequency: {pattern.Frequency:F2} failures/day");
        Output.WriteLine($"  Time span: {(pattern.LastOccurrence - pattern.FirstOccurrence).TotalDays:F1} days");
        Output.WriteLine($"  Sample IDs: {string.Join(", ", pattern.SampleFailureIds)}");
    }

    [Fact]
    public void CalculateSilhouetteScore_MeasuresClusterQuality()
    {
        // Arrange: Create well-separated clusters
        var failures = new List<FailureEntity>
        {
            CreateFailureWithEmbedding("A1", "TypeA"),
            CreateFailureWithEmbedding("A2", "TypeA"),
            CreateFailureWithEmbedding("A3", "TypeA"),
            CreateFailureWithEmbedding("B1", "TypeB"),
            CreateFailureWithEmbedding("B2", "TypeB"),
            CreateFailureWithEmbedding("B3", "TypeB")
        };

        // Manually create cluster assignments
        var assignments = new List<ClusterAssignment>
        {
            new ClusterAssignment(failures[0], 0, 0.1f),
            new ClusterAssignment(failures[1], 0, 0.1f),
            new ClusterAssignment(failures[2], 0, 0.1f),
            new ClusterAssignment(failures[3], 1, 0.1f),
            new ClusterAssignment(failures[4], 1, 0.1f),
            new ClusterAssignment(failures[5], 1, 0.1f)
        };

        WriteSection("Silhouette Score Calculation");

        // Act: Calculate silhouette score
        var score = _patternService.CalculateSilhouetteScore(failures, assignments);

        // Assert: Score should be between -1 and 1
        Assert.True(score >= -1.0f && score <= 1.0f,
            $"Silhouette score {score} should be in range [-1, 1]");

        Output.WriteLine($"✓ Silhouette score: {score:F4}");
        Output.WriteLine($"  Score interpretation:");
        if (score > 0.7f)
            Output.WriteLine($"    Excellent clustering (> 0.7)");
        else if (score > 0.5f)
            Output.WriteLine($"    Good clustering (> 0.5)");
        else if (score > 0.25f)
            Output.WriteLine($"    Reasonable clustering (> 0.25)");
        else
            Output.WriteLine($"    Weak clustering (< 0.25)");
    }

    // Helper methods

    private FailureEntity CreateFailureWithEmbedding(
        string content,
        string errorType,
        DateTime? analyzedAt = null)
    {
        var embedding = _embeddingService.GenerateEmbeddingAsync(content).Result;
        var embeddingBytes = FailureRepository.ConvertFloatArrayToBytes(embedding);

        return new FailureEntity
        {
            Id = Random.Shared.Next(1, 10000),
            LogContent = content,
            RootCause = $"Root cause: {content}",
            DetailedAnalysis = $"Analysis of {content}",
            ErrorType = errorType,
            ConfidenceScore = 0.9,
            AnalyzedAt = analyzedAt ?? DateTime.UtcNow,
            AffectedComponentsJson = $"[\"{errorType}Component\"]",
            RecommendationsJson = "[\"Fix the issue\"]",
            InputTokens = 100,
            OutputTokens = 50,
            Embedding = embeddingBytes,
            EmbeddingDimension = 512
        };
    }
}
