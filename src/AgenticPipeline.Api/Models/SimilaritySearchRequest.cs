namespace AgenticPipeline.Api.Models;

/// <summary>
/// Request model for similarity search
/// </summary>
public class SimilaritySearchRequest
{
    /// <summary>
    /// Failure ID to find similar failures for (optional)
    /// If provided, searches for failures similar to this one
    /// </summary>
    public int? FailureId { get; set; }

    /// <summary>
    /// Raw failure text to find similar failures for (optional)
    /// If provided (and FailureId is null), generates embedding and searches
    /// </summary>
    public string? FailureText { get; set; }

    /// <summary>
    /// Maximum number of similar failures to return (default: 10)
    /// </summary>
    public int TopN { get; set; } = 10;

    /// <summary>
    /// Minimum similarity threshold 0.0-1.0 (default: 0.5 = 50% similar)
    /// Only results with similarity >= this value will be returned
    /// </summary>
    public float MinSimilarity { get; set; } = 0.5f;

    /// <summary>
    /// Filter by error type (optional)
    /// </summary>
    public string? ErrorType { get; set; }

    /// <summary>
    /// Maximum number of days to look back (optional)
    /// If specified, only searches failures from the last N days
    /// </summary>
    public int? LookbackDays { get; set; }
}
