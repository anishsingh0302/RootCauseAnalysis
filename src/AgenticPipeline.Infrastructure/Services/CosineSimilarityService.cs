using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Implementation of similarity search using cosine similarity
/// Performs in-memory vector search across failure embeddings
/// </summary>
public class CosineSimilarityService : ISimilarityService
{
    private readonly ILogger<CosineSimilarityService> _logger;

    public CosineSimilarityService(ILogger<CosineSimilarityService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public float CalculateCosineSimilarity(float[] embedding1, float[] embedding2)
    {
        if (embedding1 == null || embedding2 == null)
        {
            throw new ArgumentNullException(
                embedding1 == null ? nameof(embedding1) : nameof(embedding2),
                "Embeddings cannot be null");
        }

        if (embedding1.Length != embedding2.Length)
        {
            throw new ArgumentException(
                $"Embedding dimensions must match. Got {embedding1.Length} and {embedding2.Length}");
        }

        if (embedding1.Length == 0)
        {
            throw new ArgumentException("Embeddings cannot be empty");
        }

        // Calculate dot product and magnitudes
        float dotProduct = 0.0f;
        float magnitude1 = 0.0f;
        float magnitude2 = 0.0f;

        for (int i = 0; i < embedding1.Length; i++)
        {
            dotProduct += embedding1[i] * embedding2[i];
            magnitude1 += embedding1[i] * embedding1[i];
            magnitude2 += embedding2[i] * embedding2[i];
        }

        magnitude1 = MathF.Sqrt(magnitude1);
        magnitude2 = MathF.Sqrt(magnitude2);

        // Handle zero magnitude (shouldn't happen with real embeddings)
        if (magnitude1 == 0.0f || magnitude2 == 0.0f)
        {
            _logger.LogWarning("Zero magnitude detected in embedding vectors");
            return 0.0f;
        }

        // Cosine similarity = dot product / (magnitude1 * magnitude2)
        float similarity = dotProduct / (magnitude1 * magnitude2);

        // Clamp to [-1, 1] range to handle floating point errors
        return Math.Clamp(similarity, -1.0f, 1.0f);
    }

    /// <inheritdoc/>
    public List<SimilarityResult> FindSimilarFailures(
        float[] queryEmbedding,
        List<FailureEntity> candidates,
        int topN = 10,
        float minSimilarity = 0.0f)
    {
        if (queryEmbedding == null)
        {
            throw new ArgumentNullException(nameof(queryEmbedding));
        }

        if (candidates == null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        if (topN <= 0)
        {
            throw new ArgumentException("topN must be greater than 0", nameof(topN));
        }

        _logger.LogInformation(
            "Finding similar failures. Candidates: {Count}, TopN: {TopN}, MinSimilarity: {MinSimilarity}",
            candidates.Count, topN, minSimilarity);

        var results = new List<SimilarityResult>();

        foreach (var candidate in candidates)
        {
            // Skip candidates without embeddings
            if (candidate.Embedding == null || candidate.Embedding.Length == 0)
            {
                _logger.LogDebug("Skipping candidate {Id} - no embedding", candidate.Id);
                continue;
            }

            try
            {
                // Convert byte array back to float array
                var candidateEmbedding = FailureRepository.ConvertBytesToFloatArray(candidate.Embedding);

                // Calculate similarity
                var similarity = CalculateCosineSimilarity(queryEmbedding, candidateEmbedding);

                // Filter by minimum similarity
                if (similarity >= minSimilarity)
                {
                    results.Add(new SimilarityResult(candidate, similarity));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error calculating similarity for candidate {Id}. Skipping.",
                    candidate.Id);
            }
        }

        // Sort by similarity descending and take top N
        var topResults = results
            .OrderByDescending(r => r.SimilarityScore)
            .Take(topN)
            .ToList();

        _logger.LogInformation(
            "Found {ResultCount} similar failures (filtered from {TotalCount} candidates)",
            topResults.Count, results.Count);

        return topResults;
    }

    /// <inheritdoc/>
    public List<SimilarityResult> FindSimilarFailures(
        FailureEntity failure,
        List<FailureEntity> candidates,
        int topN = 10,
        float minSimilarity = 0.0f)
    {
        if (failure == null)
        {
            throw new ArgumentNullException(nameof(failure));
        }

        if (failure.Embedding == null || failure.Embedding.Length == 0)
        {
            throw new ArgumentException(
                "Query failure must have an embedding",
                nameof(failure));
        }

        _logger.LogInformation(
            "Finding failures similar to failure {Id} (ErrorType: {ErrorType})",
            failure.Id, failure.ErrorType);

        // Convert byte array to float array
        var queryEmbedding = FailureRepository.ConvertBytesToFloatArray(failure.Embedding);

        // Filter out the query failure itself from candidates
        var filteredCandidates = candidates
            .Where(c => c.Id != failure.Id)
            .ToList();

        return FindSimilarFailures(queryEmbedding, filteredCandidates, topN, minSimilarity);
    }

    /// <inheritdoc/>
    public Dictionary<int, float> CalculateBatchSimilarity(
        float[] queryEmbedding,
        List<FailureEntity> candidates)
    {
        if (queryEmbedding == null)
        {
            throw new ArgumentNullException(nameof(queryEmbedding));
        }

        if (candidates == null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        _logger.LogInformation(
            "Calculating batch similarity for {Count} candidates",
            candidates.Count);

        var similarities = new Dictionary<int, float>();

        foreach (var candidate in candidates)
        {
            if (candidate.Embedding == null || candidate.Embedding.Length == 0)
            {
                continue;
            }

            try
            {
                var candidateEmbedding = FailureRepository.ConvertBytesToFloatArray(candidate.Embedding);
                var similarity = CalculateCosineSimilarity(queryEmbedding, candidateEmbedding);
                similarities[candidate.Id] = similarity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error calculating similarity for candidate {Id}",
                    candidate.Id);
            }
        }

        _logger.LogInformation(
            "Batch similarity completed. Calculated {Count} scores",
            similarities.Count);

        return similarities;
    }
}
