using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Implementation of pattern recognition using K-means clustering
/// </summary>
public class KMeansPatternRecognitionService : IPatternRecognitionService
{
    private readonly ILogger<KMeansPatternRecognitionService> _logger;
    private readonly ISimilarityService _similarityService;

    public KMeansPatternRecognitionService(
        ILogger<KMeansPatternRecognitionService> logger,
        ISimilarityService similarityService)
    {
        _logger = logger;
        _similarityService = similarityService;
    }

    /// <inheritdoc/>
    public PatternDetectionResult DetectPatterns(
        List<FailureEntity> failures,
        ClusteringParameters parameters)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogInformation(
            "Starting pattern detection. Failures: {Count}, K: {K}, MaxIterations: {MaxIter}",
            failures.Count, parameters.ClusterCount, parameters.MaxIterations);

        // Filter failures with embeddings
        var failuresWithEmbeddings = failures
            .Where(f => f.Embedding != null && f.Embedding.Length > 0)
            .ToList();

        if (failuresWithEmbeddings.Count == 0)
        {
            _logger.LogWarning("No failures with embeddings found");
            return new PatternDetectionResult
            {
                Patterns = new List<FailurePattern>(),
                TotalFailures = failures.Count,
                ExecutionTimeMs = 0,
                Parameters = parameters
            };
        }

        if (failuresWithEmbeddings.Count < parameters.ClusterCount)
        {
            _logger.LogWarning(
                "Fewer failures ({Count}) than requested clusters ({K}). Adjusting K.",
                failuresWithEmbeddings.Count, parameters.ClusterCount);
            parameters.ClusterCount = Math.Max(1, failuresWithEmbeddings.Count / 2);
        }

        // Convert embeddings to float arrays
        var embeddings = failuresWithEmbeddings
            .Select(f => FailureRepository.ConvertBytesToFloatArray(f.Embedding!))
            .ToList();

        // Run K-means clustering
        var (clusterAssignments, centroids) = RunKMeans(
            embeddings,
            failuresWithEmbeddings,
            parameters);

        // Generate patterns from clusters
        var patterns = GeneratePatterns(
            clusterAssignments,
            centroids,
            failuresWithEmbeddings);

        // Filter out small clusters
        patterns = patterns
            .Where(p => p.FailureCount >= parameters.MinClusterSize)
            .OrderByDescending(p => p.FailureCount)
            .ToList();

        var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

        _logger.LogInformation(
            "Pattern detection completed. Found {PatternCount} patterns in {ExecutionMs}ms",
            patterns.Count, executionTime);

        return new PatternDetectionResult
        {
            Patterns = patterns,
            TotalFailures = failuresWithEmbeddings.Count,
            ExecutionTimeMs = (long)executionTime,
            Parameters = parameters
        };
    }

    /// <inheritdoc/>
    public (FailurePattern? pattern, float similarity)? AssignToPattern(
        FailureEntity failure,
        List<FailurePattern> patterns)
    {
        if (failure.Embedding == null || failure.Embedding.Length == 0)
        {
            _logger.LogWarning("Failure {Id} has no embedding", failure.Id);
            return null;
        }

        if (patterns.Count == 0)
        {
            _logger.LogWarning("No patterns to match against");
            return null;
        }

        var failureEmbedding = FailureRepository.ConvertBytesToFloatArray(failure.Embedding);

        FailurePattern? bestPattern = null;
        float bestSimilarity = float.MinValue;

        foreach (var pattern in patterns)
        {
            if (pattern.CentroidEmbedding == null || pattern.CentroidEmbedding.Length == 0)
            {
                continue;
            }

            var centroidEmbedding = FailureRepository.ConvertBytesToFloatArray(pattern.CentroidEmbedding);
            var similarity = _similarityService.CalculateCosineSimilarity(
                failureEmbedding,
                centroidEmbedding);

            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestPattern = pattern;
            }
        }

        if (bestPattern == null)
        {
            return null;
        }

        _logger.LogInformation(
            "Assigned failure {Id} to pattern '{Pattern}' with {Similarity:P0} similarity",
            failure.Id, bestPattern.Name, bestSimilarity);

        return (bestPattern, bestSimilarity);
    }

    /// <inheritdoc/>
    public int DetermineOptimalClusterCount(List<FailureEntity> failures, int maxClusters = 10)
    {
        var failuresWithEmbeddings = failures
            .Where(f => f.Embedding != null && f.Embedding.Length > 0)
            .ToList();

        if (failuresWithEmbeddings.Count < 2)
        {
            return 1;
        }

        maxClusters = Math.Min(maxClusters, failuresWithEmbeddings.Count / 2);
        var embeddings = failuresWithEmbeddings
            .Select(f => FailureRepository.ConvertBytesToFloatArray(f.Embedding!))
            .ToList();

        var inertias = new List<float>();

        // Calculate inertia for different K values
        for (int k = 1; k <= maxClusters; k++)
        {
            var parameters = new ClusteringParameters
            {
                ClusterCount = k,
                MaxIterations = 50 // Fewer iterations for elbow method
            };

            var (assignments, centroids) = RunKMeans(embeddings, failuresWithEmbeddings, parameters);
            var inertia = CalculateInertia(embeddings, assignments, centroids);
            inertias.Add(inertia);

            _logger.LogDebug("K={K}, Inertia={Inertia:F4}", k, inertia);
        }

        // Find elbow using rate of change
        var optimalK = FindElbow(inertias);

        _logger.LogInformation("Optimal cluster count determined: K={K}", optimalK);
        return optimalK;
    }

    /// <inheritdoc/>
    public float CalculateSilhouetteScore(
        List<FailureEntity> failures,
        List<ClusterAssignment> clusterAssignments)
    {
        if (clusterAssignments.Count < 2)
        {
            return 0.0f;
        }

        var embeddings = failures
            .Select(f => FailureRepository.ConvertBytesToFloatArray(f.Embedding!))
            .ToList();

        float totalScore = 0.0f;
        int validCount = 0;

        for (int i = 0; i < clusterAssignments.Count; i++)
        {
            var assignment = clusterAssignments[i];
            var clusterMembers = clusterAssignments
                .Where(a => a.ClusterId == assignment.ClusterId)
                .ToList();

            if (clusterMembers.Count < 2)
            {
                continue; // Skip singleton clusters
            }

            // Calculate average distance to own cluster (a)
            float avgDistanceOwn = 0.0f;
            foreach (var member in clusterMembers.Where(m => m != assignment))
            {
                var memberIdx = clusterAssignments.IndexOf(member);
                var distance = 1.0f - _similarityService.CalculateCosineSimilarity(
                    embeddings[i],
                    embeddings[memberIdx]);
                avgDistanceOwn += distance;
            }
            avgDistanceOwn /= (clusterMembers.Count - 1);

            // Calculate average distance to nearest other cluster (b)
            var otherClusters = clusterAssignments
                .Select(a => a.ClusterId)
                .Distinct()
                .Where(id => id != assignment.ClusterId)
                .ToList();

            float minAvgDistanceOther = float.MaxValue;
            foreach (var otherClusterId in otherClusters)
            {
                var otherMembers = clusterAssignments
                    .Where(a => a.ClusterId == otherClusterId)
                    .ToList();

                float avgDistance = 0.0f;
                foreach (var member in otherMembers)
                {
                    var memberIdx = clusterAssignments.IndexOf(member);
                    var distance = 1.0f - _similarityService.CalculateCosineSimilarity(
                        embeddings[i],
                        embeddings[memberIdx]);
                    avgDistance += distance;
                }
                avgDistance /= otherMembers.Count;

                minAvgDistanceOther = Math.Min(minAvgDistanceOther, avgDistance);
            }

            // Silhouette score for this point
            var silhouette = (minAvgDistanceOther - avgDistanceOwn) /
                            Math.Max(avgDistanceOwn, minAvgDistanceOther);
            totalScore += silhouette;
            validCount++;
        }

        return validCount > 0 ? totalScore / validCount : 0.0f;
    }

    // Private helper methods

    private (List<ClusterAssignment> assignments, List<float[]> centroids) RunKMeans(
        List<float[]> embeddings,
        List<FailureEntity> failures,
        ClusteringParameters parameters)
    {
        var random = parameters.RandomSeed.HasValue
            ? new Random(parameters.RandomSeed.Value)
            : new Random();

        // Initialize centroids randomly
        var centroids = InitializeCentroids(embeddings, parameters.ClusterCount, random);
        var assignments = new List<ClusterAssignment>();

        int iteration = 0;
        bool converged = false;

        while (iteration < parameters.MaxIterations && !converged)
        {
            // Assignment step: assign each point to nearest centroid
            var newAssignments = new List<ClusterAssignment>();
            for (int i = 0; i < embeddings.Count; i++)
            {
                var (clusterId, distance) = FindNearestCentroid(embeddings[i], centroids);
                newAssignments.Add(new ClusterAssignment(failures[i], clusterId, distance));
            }

            // Update step: recalculate centroids
            var newCentroids = UpdateCentroids(embeddings, newAssignments, parameters.ClusterCount);

            // Check convergence
            converged = HasConverged(centroids, newCentroids, parameters.ConvergenceThreshold);

            centroids = newCentroids;
            assignments = newAssignments;
            iteration++;
        }

        _logger.LogDebug("K-means converged after {Iterations} iterations", iteration);

        return (assignments, centroids);
    }

    private List<float[]> InitializeCentroids(List<float[]> embeddings, int k, Random random)
    {
        // K-means++ initialization for better initial centroids
        var centroids = new List<float[]>();
        var indices = new HashSet<int>();

        // Choose first centroid randomly
        var firstIdx = random.Next(embeddings.Count);
        centroids.Add((float[])embeddings[firstIdx].Clone());
        indices.Add(firstIdx);

        // Choose remaining centroids based on distance to existing centroids
        for (int i = 1; i < k; i++)
        {
            var distances = new float[embeddings.Count];
            for (int j = 0; j < embeddings.Count; j++)
            {
                if (indices.Contains(j))
                {
                    distances[j] = 0.0f;
                    continue;
                }

                // Find minimum distance to existing centroids
                float minDist = float.MaxValue;
                foreach (var centroid in centroids)
                {
                    var similarity = _similarityService.CalculateCosineSimilarity(
                        embeddings[j],
                        centroid);
                    var distance = 1.0f - similarity;
                    minDist = Math.Min(minDist, distance);
                }
                distances[j] = minDist * minDist; // Square for probability weighting
            }

            // Choose next centroid with probability proportional to squared distance
            var totalDist = distances.Sum();
            if (totalDist == 0)
            {
                // Fall back to random selection
                var availableIndices = Enumerable.Range(0, embeddings.Count)
                    .Where(idx => !indices.Contains(idx))
                    .ToList();
                var nextIdx = availableIndices[random.Next(availableIndices.Count)];
                centroids.Add((float[])embeddings[nextIdx].Clone());
                indices.Add(nextIdx);
            }
            else
            {
                var threshold = random.NextSingle() * totalDist;
                float cumulative = 0.0f;
                for (int j = 0; j < distances.Length; j++)
                {
                    cumulative += distances[j];
                    if (cumulative >= threshold && !indices.Contains(j))
                    {
                        centroids.Add((float[])embeddings[j].Clone());
                        indices.Add(j);
                        break;
                    }
                }
            }
        }

        return centroids;
    }

    private (int clusterId, float distance) FindNearestCentroid(
        float[] embedding,
        List<float[]> centroids)
    {
        int nearestCluster = 0;
        float maxSimilarity = float.MinValue;

        for (int i = 0; i < centroids.Count; i++)
        {
            var similarity = _similarityService.CalculateCosineSimilarity(embedding, centroids[i]);
            if (similarity > maxSimilarity)
            {
                maxSimilarity = similarity;
                nearestCluster = i;
            }
        }

        var distance = 1.0f - maxSimilarity; // Convert similarity to distance
        return (nearestCluster, distance);
    }

    private List<float[]> UpdateCentroids(
        List<float[]> embeddings,
        List<ClusterAssignment> assignments,
        int clusterCount)
    {
        var newCentroids = new List<float[]>();
        var embeddingDim = embeddings[0].Length;

        for (int k = 0; k < clusterCount; k++)
        {
            var clusterMembers = assignments
                .Where(a => a.ClusterId == k)
                .Select(a => assignments.IndexOf(a))
                .ToList();

            if (clusterMembers.Count == 0)
            {
                // Empty cluster - keep previous centroid or reinitialize
                newCentroids.Add(new float[embeddingDim]);
                continue;
            }

            // Calculate mean of all members
            var centroid = new float[embeddingDim];
            foreach (var memberIdx in clusterMembers)
            {
                for (int d = 0; d < embeddingDim; d++)
                {
                    centroid[d] += embeddings[memberIdx][d];
                }
            }

            for (int d = 0; d < embeddingDim; d++)
            {
                centroid[d] /= clusterMembers.Count;
            }

            newCentroids.Add(centroid);
        }

        return newCentroids;
    }

    private bool HasConverged(
        List<float[]> oldCentroids,
        List<float[]> newCentroids,
        float threshold)
    {
        for (int i = 0; i < oldCentroids.Count; i++)
        {
            var similarity = _similarityService.CalculateCosineSimilarity(
                oldCentroids[i],
                newCentroids[i]);
            var movement = 1.0f - similarity;

            if (movement > threshold)
            {
                return false;
            }
        }

        return true;
    }

    private List<FailurePattern> GeneratePatterns(
        List<ClusterAssignment> assignments,
        List<float[]> centroids,
        List<FailureEntity> failures)
    {
        var patterns = new List<FailurePattern>();
        var clusterIds = assignments.Select(a => a.ClusterId).Distinct().ToList();

        foreach (var clusterId in clusterIds)
        {
            var clusterFailures = assignments
                .Where(a => a.ClusterId == clusterId)
                .Select(a => a.Failure)
                .ToList();

            if (clusterFailures.Count == 0)
            {
                continue;
            }

            // Analyze cluster characteristics
            var errorTypeCounts = clusterFailures
                .GroupBy(f => f.ErrorType)
                .OrderByDescending(g => g.Count())
                .ToList();

            var primaryErrorType = errorTypeCounts.First().Key;
            var avgConfidence = clusterFailures.Average(f => f.ConfidenceScore);

            // Calculate average intra-cluster similarity
            var clusterEmbeddings = clusterFailures
                .Select(f => FailureRepository.ConvertBytesToFloatArray(f.Embedding!))
                .ToList();

            float totalSimilarity = 0.0f;
            int pairCount = 0;
            for (int i = 0; i < clusterEmbeddings.Count; i++)
            {
                for (int j = i + 1; j < clusterEmbeddings.Count; j++)
                {
                    totalSimilarity += _similarityService.CalculateCosineSimilarity(
                        clusterEmbeddings[i],
                        clusterEmbeddings[j]);
                    pairCount++;
                }
            }
            var avgSimilarity = pairCount > 0 ? totalSimilarity / pairCount : 1.0f;

            // Extract common components
            var allComponents = clusterFailures
                .Select(f =>
                {
                    try
                    {
                        return JsonSerializer.Deserialize<List<string>>(f.AffectedComponentsJson) ?? new List<string>();
                    }
                    catch
                    {
                        return new List<string>();
                    }
                })
                .SelectMany(c => c)
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            // Extract common root causes
            var commonRootCauses = clusterFailures
                .Select(f => f.RootCause)
                .GroupBy(rc => rc)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key)
                .ToList();

            // Time range
            var firstOccurrence = clusterFailures.Min(f => f.AnalyzedAt);
            var lastOccurrence = clusterFailures.Max(f => f.AnalyzedAt);
            var daySpan = (lastOccurrence - firstOccurrence).TotalDays;
            var frequency = daySpan > 0 ? clusterFailures.Count / daySpan : clusterFailures.Count;

            var pattern = new FailurePattern
            {
                Id = clusterId,
                Name = $"Pattern {clusterId + 1}: {primaryErrorType}",
                Description = $"Cluster of {clusterFailures.Count} similar {primaryErrorType} failures",
                PrimaryErrorType = primaryErrorType,
                FailureCount = clusterFailures.Count,
                AverageConfidence = avgConfidence,
                CentroidEmbedding = FailureRepository.ConvertFloatArrayToBytes(centroids[clusterId]),
                AverageSimilarity = avgSimilarity,
                FirstOccurrence = firstOccurrence,
                LastOccurrence = lastOccurrence,
                Frequency = frequency,
                SampleFailureIds = clusterFailures.Take(5).Select(f => f.Id).ToList(),
                CommonRootCauses = commonRootCauses,
                CommonComponents = allComponents
            };

            patterns.Add(pattern);
        }

        return patterns;
    }

    private float CalculateInertia(
        List<float[]> embeddings,
        List<ClusterAssignment> assignments,
        List<float[]> centroids)
    {
        float inertia = 0.0f;
        foreach (var assignment in assignments)
        {
            var idx = assignments.IndexOf(assignment);
            var centroid = centroids[assignment.ClusterId];
            var similarity = _similarityService.CalculateCosineSimilarity(
                embeddings[idx],
                centroid);
            var distance = 1.0f - similarity;
            inertia += distance * distance;
        }
        return inertia;
    }

    private int FindElbow(List<float> inertias)
    {
        if (inertias.Count < 3)
        {
            return inertias.Count;
        }

        // Find point with maximum rate of change reduction
        var maxChangeReduction = 0.0f;
        var elbowK = 2;

        for (int k = 1; k < inertias.Count - 1; k++)
        {
            var change1 = inertias[k - 1] - inertias[k];
            var change2 = inertias[k] - inertias[k + 1];
            var changeReduction = change1 - change2;

            if (changeReduction > maxChangeReduction)
            {
                maxChangeReduction = changeReduction;
                elbowK = k + 1; // +1 because K starts at 1, not 0
            }
        }

        return elbowK;
    }
}
