namespace AgenticPipeline.Api.Models;

/// <summary>
/// Request model for pattern detection
/// </summary>
public class PatternDetectionRequest
{
    /// <summary>
    /// Number of patterns/clusters to detect (default: auto-detect using elbow method)
    /// </summary>
    public int? ClusterCount { get; set; }

    /// <summary>
    /// Maximum number of K-means iterations (default: 100)
    /// </summary>
    public int MaxIterations { get; set; } = 100;

    /// <summary>
    /// Filter by error type (optional)
    /// </summary>
    public string? ErrorType { get; set; }

    /// <summary>
    /// Maximum number of days to look back (default: 30)
    /// </summary>
    public int LookbackDays { get; set; } = 30;

    /// <summary>
    /// Minimum cluster size to keep (default: 2)
    /// </summary>
    public int MinClusterSize { get; set; } = 2;

    /// <summary>
    /// Random seed for reproducible results (optional)
    /// </summary>
    public int? RandomSeed { get; set; }
}
