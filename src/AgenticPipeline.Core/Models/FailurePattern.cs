namespace AgenticPipeline.Core.Models;

/// <summary>
/// Represents a discovered pattern (cluster) of similar failures
/// </summary>
public class FailurePattern
{
    /// <summary>
    /// Unique identifier for this pattern
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Descriptive name for the pattern
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the pattern characteristics
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Most common error type in this pattern
    /// </summary>
    public string PrimaryErrorType { get; set; } = string.Empty;

    /// <summary>
    /// Number of failures in this pattern
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Average confidence score of failures in this pattern
    /// </summary>
    public double AverageConfidence { get; set; }

    /// <summary>
    /// Centroid embedding vector for this pattern
    /// </summary>
    public byte[]? CentroidEmbedding { get; set; }

    /// <summary>
    /// Average intra-cluster similarity (cohesion metric)
    /// </summary>
    public float AverageSimilarity { get; set; }

    /// <summary>
    /// When this pattern was first detected
    /// </summary>
    public DateTime FirstOccurrence { get; set; }

    /// <summary>
    /// When this pattern was last seen
    /// </summary>
    public DateTime LastOccurrence { get; set; }

    /// <summary>
    /// Frequency of this pattern (occurrences per day)
    /// </summary>
    public double Frequency { get; set; }

    /// <summary>
    /// Sample failure IDs that represent this pattern
    /// </summary>
    public List<int> SampleFailureIds { get; set; } = new List<int>();

    /// <summary>
    /// Common root causes in this pattern
    /// </summary>
    public List<string> CommonRootCauses { get; set; } = new List<string>();

    /// <summary>
    /// Common affected components
    /// </summary>
    public List<string> CommonComponents { get; set; } = new List<string>();
}

/// <summary>
/// Result of pattern detection analysis
/// </summary>
public class PatternDetectionResult
{
    /// <summary>
    /// Detected patterns/clusters
    /// </summary>
    public required List<FailurePattern> Patterns { get; set; }

    /// <summary>
    /// Total number of failures analyzed
    /// </summary>
    public int TotalFailures { get; set; }

    /// <summary>
    /// Number of clusters found
    /// </summary>
    public int ClusterCount => Patterns.Count;

    /// <summary>
    /// Average cluster size
    /// </summary>
    public double AverageClusterSize => TotalFailures > 0
        ? (double)Patterns.Sum(p => p.FailureCount) / ClusterCount
        : 0;

    /// <summary>
    /// Analysis execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Algorithm parameters used
    /// </summary>
    public required ClusteringParameters Parameters { get; set; }
}

/// <summary>
/// Parameters for clustering algorithm
/// </summary>
public class ClusteringParameters
{
    /// <summary>
    /// Number of clusters to create (K in K-means)
    /// </summary>
    public int ClusterCount { get; set; } = 5;

    /// <summary>
    /// Maximum number of iterations
    /// </summary>
    public int MaxIterations { get; set; } = 100;

    /// <summary>
    /// Convergence threshold (stop when centroids move less than this)
    /// </summary>
    public float ConvergenceThreshold { get; set; } = 0.001f;

    /// <summary>
    /// Random seed for reproducible results
    /// </summary>
    public int? RandomSeed { get; set; }

    /// <summary>
    /// Minimum cluster size (clusters smaller than this are merged or discarded)
    /// </summary>
    public int MinClusterSize { get; set; } = 2;
}

/// <summary>
/// Assignment of a failure to a cluster
/// </summary>
public record ClusterAssignment(
    FailureEntity Failure,
    int ClusterId,
    float DistanceToCentroid);
