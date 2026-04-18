using AgenticPipeline.Core.Models;

namespace AgenticPipeline.Core.Interfaces;

/// <summary>
/// Service for detecting patterns in failure data using clustering algorithms
/// </summary>
public interface IPatternRecognitionService
{
    /// <summary>
    /// Detects patterns in a collection of failures using K-means clustering
    /// </summary>
    /// <param name="failures">Failures to analyze (must have embeddings)</param>
    /// <param name="parameters">Clustering parameters</param>
    /// <returns>Detected patterns with cluster information</returns>
    PatternDetectionResult DetectPatterns(
        List<FailureEntity> failures,
        ClusteringParameters parameters);

    /// <summary>
    /// Assigns a new failure to the most similar existing pattern
    /// </summary>
    /// <param name="failure">Failure to classify (must have embedding)</param>
    /// <param name="patterns">Existing patterns to match against</param>
    /// <returns>Best matching pattern and similarity score, or null if no good match</returns>
    (FailurePattern? pattern, float similarity)? AssignToPattern(
        FailureEntity failure,
        List<FailurePattern> patterns);

    /// <summary>
    /// Calculates the optimal number of clusters using the elbow method
    /// </summary>
    /// <param name="failures">Failures to analyze</param>
    /// <param name="maxClusters">Maximum number of clusters to test</param>
    /// <returns>Recommended number of clusters</returns>
    int DetermineOptimalClusterCount(
        List<FailureEntity> failures,
        int maxClusters = 10);

    /// <summary>
    /// Evaluates cluster quality using silhouette score
    /// </summary>
    /// <param name="failures">Failures with cluster assignments</param>
    /// <param name="clusterAssignments">Cluster assignments for each failure</param>
    /// <returns>Silhouette score between -1 and 1 (higher is better)</returns>
    float CalculateSilhouetteScore(
        List<FailureEntity> failures,
        List<ClusterAssignment> clusterAssignments);
}
