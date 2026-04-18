using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Services;
using AgenticPipeline.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace AgenticPipeline.IntegrationTests;

/// <summary>
/// Integration tests for embedding generation service
/// </summary>
[Trait("Category", "Integration")]
[Trait("RequiresAWS", "true")]
public class EmbeddingServiceTests : IntegrationTestBase
{
    public EmbeddingServiceTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task GenerateEmbedding_WithValidText_ReturnsEmbeddingVector()
    {
        // Arrange: Setup AWS credentials and create embedding service
        AwsCredentialHelper.EnsureCredentials();

        var settings = Options.Create(new EmbeddingSettings());
        var logger = LoggerFactory.CreateLogger<TitanEmbeddingService>();
        var embeddingService = new TitanEmbeddingService(settings, logger);

        var testText = "NullReferenceException in PaymentService.ProcessPayment() at line 42";

        WriteSection("Embedding Generation Test");
        Output.WriteLine($"Input text: {testText}");
        Output.WriteLine($"Expected dimensions: {settings.Value.Dimensions}");

        // Act: Generate embedding
        var startTime = DateTime.UtcNow;
        var embedding = await embeddingService.GenerateEmbeddingAsync(testText);
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert: Verify embedding generated correctly
        Assert.NotNull(embedding);
        Assert.Equal(settings.Value.Dimensions, embedding.Length);
        Assert.All(embedding, value => Assert.True(value >= -1.0f && value <= 1.0f));

        Output.WriteLine($"Embedding generated successfully");
        Output.WriteLine($"Dimensions: {embedding.Length}");
        Output.WriteLine($"Time taken: {elapsed}ms");
        Output.WriteLine($"First 5 values: [{string.Join(", ", embedding.Take(5).Select(v => v.ToString("F4")))}]");
        Output.WriteLine($"Performance: {(elapsed < 2000 ? "Excellent" : "Needs optimization")}");
    }

    [Fact]
    public async Task GenerateEmbedding_WithLongText_TruncatesAndGenerates()
    {
        // Arrange: Create text longer than max input length
        AwsCredentialHelper.EnsureCredentials();

        var settings = Options.Create(new EmbeddingSettings());
        var logger = LoggerFactory.CreateLogger<TitanEmbeddingService>();
        var embeddingService = new TitanEmbeddingService(settings, logger);

        var longText = string.Join(" ", Enumerable.Repeat("This is a test error message.", 1000));
        Output.WriteLine($"Input text length: {longText.Length} characters");
        Output.WriteLine($"Max input length: {settings.Value.MaxInputLength} characters");

        // Act: Generate embedding for long text
        var embedding = await embeddingService.GenerateEmbeddingAsync(longText);

        // Assert: Verify embedding generated (text was truncated internally)
        Assert.NotNull(embedding);
        Assert.Equal(settings.Value.Dimensions, embedding.Length);
        Output.WriteLine("Long text handled successfully with truncation");
    }

    [Fact]
    public async Task GenerateEmbedding_WithEmptyText_ThrowsArgumentException()
    {
        // Arrange: Create embedding service
        AwsCredentialHelper.EnsureCredentials();

        var settings = Options.Create(new EmbeddingSettings());
        var logger = LoggerFactory.CreateLogger<TitanEmbeddingService>();
        var embeddingService = new TitanEmbeddingService(settings, logger);

        // Act & Assert: Verify empty text throws exception
        await Assert.ThrowsAsync<ArgumentException>(
            () => embeddingService.GenerateEmbeddingAsync(""));

        Output.WriteLine("Empty text correctly rejected with ArgumentException");
    }

    [Fact]
    public async Task GenerateEmbeddings_WithMultipleTexts_ReturnsBatchEmbeddings()
    {
        // Arrange: Setup multiple test texts
        AwsCredentialHelper.EnsureCredentials();

        var settings = Options.Create(new EmbeddingSettings());
        var logger = LoggerFactory.CreateLogger<TitanEmbeddingService>();
        var embeddingService = new TitanEmbeddingService(settings, logger);

        var texts = new List<string>
        {
            "NullReferenceException in PaymentService",
            "TimeoutException in DatabaseConnection",
            "ArgumentException in UserValidation"
        };

        WriteSection("Batch Embedding Generation Test");
        Output.WriteLine($"Generating embeddings for {texts.Count} texts");

        // Act: Generate batch embeddings
        var startTime = DateTime.UtcNow;
        var embeddings = await embeddingService.GenerateEmbeddingsAsync(texts);
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert: Verify all embeddings generated
        Assert.NotNull(embeddings);
        Assert.Equal(texts.Count, embeddings.Count);
        Assert.All(embeddings, embedding =>
        {
            Assert.NotNull(embedding);
            Assert.Equal(settings.Value.Dimensions, embedding.Length);
        });

        Output.WriteLine($"Batch completed successfully");
        Output.WriteLine($"Total time: {elapsed}ms");
        Output.WriteLine($"Average per text: {elapsed / texts.Count:F2}ms");
        Output.WriteLine($"Performance: {(elapsed < 10000 ? "Excellent" : "Acceptable")}");
    }

    [Fact]
    public async Task GenerateEmbedding_WithDifferentTexts_ProducesDifferentEmbeddings()
    {
        // Arrange: Create two different texts
        AwsCredentialHelper.EnsureCredentials();

        var settings = Options.Create(new EmbeddingSettings());
        var logger = LoggerFactory.CreateLogger<TitanEmbeddingService>();
        var embeddingService = new TitanEmbeddingService(settings, logger);

        var text1 = "NullReferenceException in PaymentService";
        var text2 = "TimeoutException in DatabaseConnection";

        WriteSection("Embedding Uniqueness Test");

        // Act: Generate embeddings for both texts
        var embedding1 = await embeddingService.GenerateEmbeddingAsync(text1);
        var embedding2 = await embeddingService.GenerateEmbeddingAsync(text2);

        // Assert: Verify embeddings are different
        Assert.NotNull(embedding1);
        Assert.NotNull(embedding2);
        Assert.Equal(embedding1.Length, embedding2.Length);

        // Calculate simple difference (should be significant)
        var totalDiff = 0.0;
        for (int i = 0; i < embedding1.Length; i++)
        {
            totalDiff += Math.Abs(embedding1[i] - embedding2[i]);
        }
        var avgDiff = totalDiff / embedding1.Length;

        Output.WriteLine($"Text 1: {text1}");
        Output.WriteLine($"Text 2: {text2}");
        Output.WriteLine($"Average difference per dimension: {avgDiff:F6}");
        Output.WriteLine($"Embeddings are clearly different: {avgDiff > 0.001}");

        Assert.True(avgDiff > 0.001, "Embeddings should be significantly different for different texts");
    }

    [Fact]
    public async Task EmbeddingDimension_ReturnsConfiguredValue()
    {
        // Arrange: Create embedding service with specific dimensions
        AwsCredentialHelper.EnsureCredentials();

        var settings = Options.Create(new EmbeddingSettings(
            ModelId: "amazon.titan-embed-text-v2:0",
            Dimensions: 512,
            MaxInputLength: 8192,
            NormalizeEmbeddings: true));

        var logger = LoggerFactory.CreateLogger<TitanEmbeddingService>();
        var embeddingService = new TitanEmbeddingService(settings, logger);

        // Act: Get embedding dimension
        var dimension = embeddingService.EmbeddingDimension;

        // Assert: Verify dimension matches configuration
        Assert.Equal(512, dimension);
        Output.WriteLine($"Embedding dimension: {dimension}");
    }
}
