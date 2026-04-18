using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Repositories;
using AgenticPipeline.Infrastructure.Services;
using AgenticPipeline.Tests.Common;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AgenticPipeline.IntegrationTests;

/// <summary>
/// Integration tests for similarity search functionality
/// </summary>
[Trait("Category", "Integration")]
public class SimilarityServiceTests : IntegrationTestBase
{
    private readonly CosineSimilarityService _similarityService;

    public SimilarityServiceTests(ITestOutputHelper output) : base(output)
    {
        var logger = LoggerFactory.CreateLogger<CosineSimilarityService>();
        _similarityService = new CosineSimilarityService(logger);
    }

    [Fact]
    public void CalculateCosineSimilarity_IdenticalVectors_Returns1()
    {
        // Arrange: Create identical vectors
        var vector = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };

        WriteSection("Identical Vectors Test");

        // Act: Calculate similarity
        var similarity = _similarityService.CalculateCosineSimilarity(vector, vector);

        // Assert: Should be exactly 1.0 (100% similar)
        Assert.Equal(1.0f, similarity, precision: 5);

        Output.WriteLine($"✓ Identical vectors similarity: {similarity:F6} (100%)");
    }

    [Fact]
    public void CalculateCosineSimilarity_OrthogonalVectors_Returns0()
    {
        // Arrange: Create orthogonal vectors (perpendicular in vector space)
        var vector1 = new float[] { 1.0f, 0.0f, 0.0f };
        var vector2 = new float[] { 0.0f, 1.0f, 0.0f };

        WriteSection("Orthogonal Vectors Test");

        // Act: Calculate similarity
        var similarity = _similarityService.CalculateCosineSimilarity(vector1, vector2);

        // Assert: Should be 0.0 (completely different)
        Assert.Equal(0.0f, similarity, precision: 5);

        Output.WriteLine($"✓ Orthogonal vectors similarity: {similarity:F6} (0%)");
    }

    [Fact]
    public void CalculateCosineSimilarity_OppositeVectors_ReturnsNegative1()
    {
        // Arrange: Create opposite vectors
        var vector1 = new float[] { 1.0f, 2.0f, 3.0f };
        var vector2 = new float[] { -1.0f, -2.0f, -3.0f };

        WriteSection("Opposite Vectors Test");

        // Act: Calculate similarity
        var similarity = _similarityService.CalculateCosineSimilarity(vector1, vector2);

        // Assert: Should be -1.0 (completely opposite)
        Assert.Equal(-1.0f, similarity, precision: 5);

        Output.WriteLine($"✓ Opposite vectors similarity: {similarity:F6} (-100%)");
    }

    [Fact]
    public void CalculateCosineSimilarity_SimilarVectors_ReturnsHighSimilarity()
    {
        // Arrange: Create similar but not identical vectors
        var vector1 = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var vector2 = new float[] { 1.1f, 2.1f, 2.9f, 4.0f };

        WriteSection("Similar Vectors Test");

        // Act: Calculate similarity
        var similarity = _similarityService.CalculateCosineSimilarity(vector1, vector2);

        // Assert: Should be close to 1.0
        Assert.True(similarity > 0.99f, $"Similarity {similarity} should be > 0.99");

        Output.WriteLine($"✓ Similar vectors similarity: {similarity:F6} ({similarity * 100:F2}%)");
    }

    [Fact]
    public void CalculateCosineSimilarity_DifferentDimensions_ThrowsException()
    {
        // Arrange: Create vectors with different dimensions
        var vector1 = new float[] { 1.0f, 2.0f, 3.0f };
        var vector2 = new float[] { 1.0f, 2.0f };

        WriteSection("Dimension Mismatch Test");

        // Act & Assert: Should throw ArgumentException
        var exception = Assert.Throws<ArgumentException>(() =>
            _similarityService.CalculateCosineSimilarity(vector1, vector2));

        Assert.Contains("dimensions must match", exception.Message, StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"✓ Exception thrown correctly for mismatched dimensions");
        Output.WriteLine($"  Message: {exception.Message}");
    }

    [Fact]
    public void FindSimilarFailures_WithMultipleCandidates_ReturnsTopNRanked()
    {
        // Arrange: Create query embedding and candidate failures
        var mockEmbedding = new MockEmbeddingService(seed: 42);
        var queryEmbedding = mockEmbedding.GenerateEmbeddingAsync("NullReferenceException in PaymentService").Result;

        var candidates = new List<FailureEntity>
        {
            CreateFailureWithEmbedding(1, "NullReferenceException in PaymentService", mockEmbedding),
            CreateFailureWithEmbedding(2, "NullReferenceException in OrderService", mockEmbedding),
            CreateFailureWithEmbedding(3, "TimeoutException in ApiClient", mockEmbedding),
            CreateFailureWithEmbedding(4, "OutOfMemoryException in DataProcessor", mockEmbedding),
            CreateFailureWithEmbedding(5, "NullReferenceException in UserService", mockEmbedding)
        };

        WriteSection("Top N Ranking Test");
        Output.WriteLine($"Query: NullReferenceException in PaymentService");
        Output.WriteLine($"Candidates: {candidates.Count}");

        // Act: Find top 3 similar failures
        var results = _similarityService.FindSimilarFailures(queryEmbedding, candidates, topN: 3);

        // Assert: Verify results
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);

        // Results should be sorted by similarity descending
        for (int i = 0; i < results.Count - 1; i++)
        {
            Assert.True(results[i].SimilarityScore >= results[i + 1].SimilarityScore,
                $"Result {i} score {results[i].SimilarityScore} should be >= result {i + 1} score {results[i + 1].SimilarityScore}");
        }

        Output.WriteLine($"✓ Found {results.Count} similar failures (ranked):");
        foreach (var (result, index) in results.Select((r, i) => (r, i)))
        {
            Output.WriteLine($"  {index + 1}. Failure {result.Failure.Id}: {result.Failure.ErrorType}");
            Output.WriteLine($"     Similarity: {result.SimilarityScore:F4} ({result.SimilarityPercentage:F1}%)");
        }
    }

    [Fact]
    public void FindSimilarFailures_WithMinSimilarityThreshold_FiltersResults()
    {
        // Arrange: Create diverse failures
        var mockEmbedding = new MockEmbeddingService(seed: 123);
        var queryEmbedding = mockEmbedding.GenerateEmbeddingAsync("Database connection timeout").Result;

        var candidates = new List<FailureEntity>
        {
            CreateFailureWithEmbedding(1, "Database connection timeout error", mockEmbedding),
            CreateFailureWithEmbedding(2, "Database query timeout", mockEmbedding),
            CreateFailureWithEmbedding(3, "Connection pool exhausted", mockEmbedding),
            CreateFailureWithEmbedding(4, "NullReferenceException in unrelated service", mockEmbedding),
            CreateFailureWithEmbedding(5, "Compilation error in build", mockEmbedding)
        };

        WriteSection("Minimum Similarity Threshold Test");

        // Act: Find similar failures with high threshold
        var results = _similarityService.FindSimilarFailures(
            queryEmbedding,
            candidates,
            topN: 10,
            minSimilarity: 0.8f);

        // Assert: Should filter out low similarity results
        Assert.NotNull(results);
        Assert.All(results, r => Assert.True(r.SimilarityScore >= 0.8f,
            $"Result score {r.SimilarityScore} should be >= 0.8"));

        Output.WriteLine($"✓ Found {results.Count} failures above 80% similarity threshold");
        foreach (var result in results)
        {
            Output.WriteLine($"  Failure {result.Failure.Id}: {result.SimilarityPercentage:F1}%");
        }
    }

    [Fact]
    public void FindSimilarFailures_WithNoEmbeddings_ReturnsEmpty()
    {
        // Arrange: Create failures without embeddings
        var queryEmbedding = new float[] { 1.0f, 2.0f, 3.0f };
        var candidates = new List<FailureEntity>
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

        WriteSection("No Embeddings Test");

        // Act: Find similar failures
        var results = _similarityService.FindSimilarFailures(queryEmbedding, candidates, topN: 10);

        // Assert: Should return empty list
        Assert.NotNull(results);
        Assert.Empty(results);

        Output.WriteLine($"✓ Correctly returned empty list when candidates have no embeddings");
    }

    [Fact]
    public void CalculateBatchSimilarity_WithMultipleCandidates_ReturnsAllScores()
    {
        // Arrange: Create query and candidates
        var mockEmbedding = new MockEmbeddingService(seed: 456);
        var queryEmbedding = mockEmbedding.GenerateEmbeddingAsync("Test query").Result;

        var candidates = new List<FailureEntity>
        {
            CreateFailureWithEmbedding(1, "Test case 1", mockEmbedding),
            CreateFailureWithEmbedding(2, "Test case 2", mockEmbedding),
            CreateFailureWithEmbedding(3, "Test case 3", mockEmbedding)
        };

        WriteSection("Batch Similarity Calculation Test");

        // Act: Calculate batch similarities
        var scores = _similarityService.CalculateBatchSimilarity(queryEmbedding, candidates);

        // Assert: Should have score for each candidate
        Assert.NotNull(scores);
        Assert.Equal(3, scores.Count);
        Assert.All(candidates, c => Assert.True(scores.ContainsKey(c.Id)));

        Output.WriteLine($"✓ Calculated {scores.Count} similarity scores:");
        foreach (var (id, score) in scores.OrderByDescending(kvp => kvp.Value))
        {
            Output.WriteLine($"  Failure {id}: {score:F4} ({score * 100:F1}%)");
        }
    }

    // Helper method to create failure with embedding
    private FailureEntity CreateFailureWithEmbedding(int id, string content, MockEmbeddingService embeddingService)
    {
        var embedding = embeddingService.GenerateEmbeddingAsync(content).Result;
        var embeddingBytes = FailureRepository.ConvertFloatArrayToBytes(embedding);

        return new FailureEntity
        {
            Id = id,
            LogContent = content,
            RootCause = $"Root cause for {content}",
            DetailedAnalysis = $"Analysis of {content}",
            ErrorType = content.Split(' ').First(),
            ConfidenceScore = 0.9,
            AnalyzedAt = DateTime.UtcNow.AddDays(-id),
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 100,
            OutputTokens = 50,
            Embedding = embeddingBytes,
            EmbeddingDimension = 512
        };
    }
}
