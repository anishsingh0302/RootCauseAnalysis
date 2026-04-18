using AgenticPipeline.Core.Models;

namespace AgenticPipeline.Core.Interfaces;

/// <summary>
/// Service for calculating vector similarity between failure embeddings
/// </summary>
public interface ISimilarityService
{
    /// <summary>
    /// Calculates cosine similarity between two embedding vectors
    /// </summary>
    /// <param name="embedding1">First embedding vector</param>
    /// <param name="embedding2">Second embedding vector</param>
    /// <returns>Cosine similarity score between -1 and 1 (higher is more similar)</returns>
    float CalculateCosineSimilarity(float[] embedding1, float[] embedding2);

    /// <summary>
    /// Finds the most similar failures to the given embedding
    /// </summary>
    /// <param name="queryEmbedding">Query embedding vector</param>
    /// <param name="candidates">Candidate failures to compare against</param>
    /// <param name="topN">Number of top results to return</param>
    /// <param name="minSimilarity">Minimum similarity threshold (0.0 to 1.0)</param>
    /// <returns>List of similar failures with similarity scores, sorted by score descending</returns>
    List<SimilarityResult> FindSimilarFailures(
        float[] queryEmbedding,
        List<FailureEntity> candidates,
        int topN = 10,
        float minSimilarity = 0.0f);

    /// <summary>
    /// Finds the most similar failures to the given failure
    /// </summary>
    /// <param name="failure">Query failure (must have embedding)</param>
    /// <param name="candidates">Candidate failures to compare against</param>
    /// <param name="topN">Number of top results to return</param>
    /// <param name="minSimilarity">Minimum similarity threshold (0.0 to 1.0)</param>
    /// <returns>List of similar failures with similarity scores, sorted by score descending</returns>
    List<SimilarityResult> FindSimilarFailures(
        FailureEntity failure,
        List<FailureEntity> candidates,
        int topN = 10,
        float minSimilarity = 0.0f);

    /// <summary>
    /// Calculates similarity scores for all candidate failures
    /// </summary>
    /// <param name="queryEmbedding">Query embedding vector</param>
    /// <param name="candidates">Candidate failures to score</param>
    /// <returns>Dictionary mapping failure ID to similarity score</returns>
    Dictionary<int, float> CalculateBatchSimilarity(
        float[] queryEmbedding,
        List<FailureEntity> candidates);
}

/// <summary>
/// Represents a failure with its similarity score to a query
/// </summary>
public record SimilarityResult(
    FailureEntity Failure,
    float SimilarityScore)
{
    /// <summary>
    /// Similarity score as percentage (0-100)
    /// </summary>
    public float SimilarityPercentage => SimilarityScore * 100f;
}
