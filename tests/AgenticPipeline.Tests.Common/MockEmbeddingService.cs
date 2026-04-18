using AgenticPipeline.Core.Interfaces;

namespace AgenticPipeline.Tests.Common;

/// <summary>
/// Mock embedding service for unit and integration tests
/// Returns deterministic fake embeddings without calling external APIs
/// </summary>
public class MockEmbeddingService : IEmbeddingService
{
    private readonly int _dimension;
    private readonly Random _random;

    public MockEmbeddingService(int dimension = 512, int? seed = null)
    {
        _dimension = dimension;
        _random = seed.HasValue ? new Random(seed.Value) : new Random(42); // Default seed for deterministic tests
    }

    public int EmbeddingDimension => _dimension;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        // Generate deterministic fake embedding based on text hash
        var textHash = text.GetHashCode();
        var localRandom = new Random(textHash);

        var embedding = new float[_dimension];
        for (int i = 0; i < _dimension; i++)
        {
            embedding[i] = (float)(localRandom.NextDouble() * 2.0 - 1.0); // Range: -1.0 to 1.0
        }

        return Task.FromResult(embedding);
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(
        List<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts == null || texts.Count == 0)
        {
            throw new ArgumentException("Texts list cannot be null or empty", nameof(texts));
        }

        var embeddings = new List<float[]>();
        foreach (var text in texts)
        {
            var embedding = await GenerateEmbeddingAsync(text, cancellationToken);
            embeddings.Add(embedding);
        }

        return embeddings;
    }
}
