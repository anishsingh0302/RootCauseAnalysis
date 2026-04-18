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
/// Integration tests for automatic embedding generation in FailureRepository
/// </summary>
[Trait("Category", "Integration")]
public class FailureEmbeddingTests : IntegrationTestBase, IDisposable
{
    private readonly AgenticPipelineDbContext _context;
    private readonly IFailureRepository _repository;
    private readonly string _dbPath;

    public FailureEmbeddingTests(ITestOutputHelper output) : base(output)
    {
        // Setup unique test database
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_embedding_{Guid.NewGuid()}.db");
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
    public async Task SaveFailure_AutoGeneratesEmbedding()
    {
        // Arrange: Create failure without embedding
        var failure = new FailureEntity
        {
            LogContent = "NullReferenceException at PaymentService.ProcessPayment()",
            RootCause = "Null payment object passed to ProcessPayment",
            DetailedAnalysis = "The payment object was null when ProcessPayment was called",
            ErrorType = "NullReferenceException",
            ConfidenceScore = 0.95,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[\"PaymentService\"]",
            RecommendationsJson = "[\"Add null check\"]",
            InputTokens = 100,
            OutputTokens = 50
        };

        WriteSection("Embedding Auto-Generation Test");
        Output.WriteLine("Saving failure without explicit embedding...");

        // Act: Save failure (embedding should be auto-generated)
        var saved = await _repository.SaveFailureAsync(failure);

        // Assert: Verify embedding was generated
        Assert.NotNull(saved.Embedding);
        Assert.NotEmpty(saved.Embedding);
        Assert.Equal(512, saved.EmbeddingDimension);

        // Verify embedding size (512 floats * 4 bytes = 2048 bytes)
        Assert.Equal(2048, saved.Embedding.Length);

        Output.WriteLine($"✓ Embedding auto-generated");
        Output.WriteLine($"  Dimensions: {saved.EmbeddingDimension}");
        Output.WriteLine($"  Storage size: {saved.Embedding.Length} bytes");

        // Verify retrieval preserves embedding
        var retrieved = await _repository.GetByIdAsync(saved.Id);
        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.Embedding);
        Assert.Equal(2048, retrieved.Embedding.Length);
        Assert.Equal(512, retrieved.EmbeddingDimension);

        Output.WriteLine($"✓ Embedding persisted and retrieved correctly");
    }

    [Fact]
    public async Task SaveFailure_WithExistingEmbedding_DoesNotOverwrite()
    {
        // Arrange: Create failure with pre-existing embedding
        var existingEmbedding = new byte[] { 1, 2, 3, 4, 5 };
        var failure = new FailureEntity
        {
            LogContent = "Test error",
            RootCause = "Test cause",
            DetailedAnalysis = "Test analysis",
            ErrorType = "TestError",
            ConfidenceScore = 0.8,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[]",
            RecommendationsJson = "[]",
            InputTokens = 50,
            OutputTokens = 25,
            Embedding = existingEmbedding,
            EmbeddingDimension = 256
        };

        WriteSection("Existing Embedding Preservation Test");
        Output.WriteLine("Saving failure with pre-existing embedding...");

        // Act: Save failure
        var saved = await _repository.SaveFailureAsync(failure);

        // Assert: Verify existing embedding was preserved
        Assert.NotNull(saved.Embedding);
        Assert.Equal(existingEmbedding, saved.Embedding);
        Assert.Equal(256, saved.EmbeddingDimension);

        Output.WriteLine($"✓ Pre-existing embedding preserved");
        Output.WriteLine($"  Original bytes: {existingEmbedding.Length}");
        Output.WriteLine($"  Saved bytes: {saved.Embedding.Length}");
    }

    [Fact]
    public async Task SaveFailureWithCommits_AutoGeneratesEmbedding()
    {
        // Arrange: Create failure and commits
        var failure = new FailureEntity
        {
            LogContent = "Build failed: Compilation error in OrderService",
            RootCause = "Missing semicolon on line 42",
            DetailedAnalysis = "Syntax error: expected ';' at end of statement",
            ErrorType = "CompilationError",
            ConfidenceScore = 0.99,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[\"OrderService.cs\"]",
            RecommendationsJson = "[\"Add missing semicolon\"]",
            InputTokens = 120,
            OutputTokens = 60
        };

        var commits = new List<CommitEntity>
        {
            new CommitEntity
            {
                Sha = "abc123",
                Message = "Update OrderService",
                Author = "developer1",
                AuthorEmail = "developer1@example.com",
                CommitDate = DateTime.UtcNow.AddHours(-2),
                FilesChangedJson = "[\"OrderService.cs\", \"OrderService.Tests.cs\"]",
                ChangeType = "modified",
                Additions = 15,
                Deletions = 3,
                Url = "https://github.com/test/repo/commit/abc123",
                ConfidenceScore = 0.85
            }
        };

        WriteSection("Embedding with Commits Test");
        Output.WriteLine($"Saving failure with {commits.Count} related commits...");

        // Act: Save failure with commits
        var saved = await _repository.SaveFailureWithCommitsAsync(failure, commits);

        // Assert: Verify embedding was generated
        Assert.NotNull(saved.Embedding);
        Assert.Equal(512, saved.EmbeddingDimension);
        Assert.Equal(2048, saved.Embedding.Length);

        // Verify commits were saved
        var retrieved = await _repository.GetByIdAsync(saved.Id, includeCommits: true);
        Assert.NotNull(retrieved);
        Assert.Single(retrieved.Commits);
        Assert.NotNull(retrieved.Embedding);

        Output.WriteLine($"✓ Embedding generated with commits");
        Output.WriteLine($"  Commits saved: {retrieved.Commits.Count}");
        Output.WriteLine($"  Embedding dimensions: {retrieved.EmbeddingDimension}");
    }

    [Fact]
    public async Task SaveMultipleFailures_GeneratesUniqueEmbeddings()
    {
        // Arrange: Create two failures with different content
        var failure1 = new FailureEntity
        {
            LogContent = "OutOfMemoryException in DataProcessor",
            RootCause = "Memory leak in data processing loop",
            DetailedAnalysis = "Large data structures not being disposed",
            ErrorType = "OutOfMemoryException",
            ConfidenceScore = 0.88,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[\"DataProcessor\"]",
            RecommendationsJson = "[\"Implement IDisposable\"]",
            InputTokens = 150,
            OutputTokens = 75
        };

        var failure2 = new FailureEntity
        {
            LogContent = "TimeoutException in ApiClient",
            RootCause = "API endpoint not responding",
            DetailedAnalysis = "External service timed out after 30 seconds",
            ErrorType = "TimeoutException",
            ConfidenceScore = 0.92,
            AnalyzedAt = DateTime.UtcNow,
            AffectedComponentsJson = "[\"ApiClient\"]",
            RecommendationsJson = "[\"Add retry logic\"]",
            InputTokens = 130,
            OutputTokens = 65
        };

        WriteSection("Unique Embeddings Test");
        Output.WriteLine("Saving two different failures...");

        // Act: Save both failures
        var saved1 = await _repository.SaveFailureAsync(failure1);
        var saved2 = await _repository.SaveFailureAsync(failure2);

        // Assert: Verify both have embeddings
        Assert.NotNull(saved1.Embedding);
        Assert.NotNull(saved2.Embedding);
        Assert.Equal(2048, saved1.Embedding.Length);
        Assert.Equal(2048, saved2.Embedding.Length);

        // Verify embeddings are different (deterministic mock should give different results for different texts)
        Assert.NotEqual(saved1.Embedding, saved2.Embedding);

        Output.WriteLine($"✓ Both failures have unique embeddings");
        Output.WriteLine($"  Failure 1 embedding: {saved1.Embedding.Length} bytes");
        Output.WriteLine($"  Failure 2 embedding: {saved2.Embedding.Length} bytes");
        Output.WriteLine($"  Embeddings are different: {!saved1.Embedding.SequenceEqual(saved2.Embedding)}");
    }

    [Fact]
    public async Task ConvertEmbeddingBytes_RoundTripConversion_MaintainsValues()
    {
        // Arrange: Create original embedding values
        var originalFloats = new float[] { -0.5f, 0.0f, 0.25f, 1.0f, -1.0f };

        WriteSection("Embedding Byte Conversion Test");
        Output.WriteLine($"Original values: [{string.Join(", ", originalFloats)}]");

        // Act: Convert to bytes and back
        var bytes = FailureRepository.ConvertFloatArrayToBytes(originalFloats);
        var convertedFloats = FailureRepository.ConvertBytesToFloatArray(bytes);

        // Assert: Verify values match
        Assert.Equal(originalFloats.Length, convertedFloats.Length);
        for (int i = 0; i < originalFloats.Length; i++)
        {
            Assert.Equal(originalFloats[i], convertedFloats[i], precision: 6);
        }

        Output.WriteLine($"✓ Round-trip conversion successful");
        Output.WriteLine($"  Original length: {originalFloats.Length} floats");
        Output.WriteLine($"  Byte length: {bytes.Length} bytes");
        Output.WriteLine($"  Converted length: {convertedFloats.Length} floats");
        Output.WriteLine($"  Converted values: [{string.Join(", ", convertedFloats)}]");
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
