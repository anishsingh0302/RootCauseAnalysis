namespace AgenticPipeline.Api.Models;

/// <summary>
/// Response model for similarity search
/// </summary>
public class SimilaritySearchResponse
{
    /// <summary>
    /// Query information
    /// </summary>
    public required QueryInfo Query { get; set; }

    /// <summary>
    /// List of similar failures ranked by similarity
    /// </summary>
    public required List<SimilarFailureResult> Results { get; set; }

    /// <summary>
    /// Total number of candidates searched
    /// </summary>
    public int TotalCandidates { get; set; }

    /// <summary>
    /// Number of results returned
    /// </summary>
    public int ResultCount => Results.Count;

    /// <summary>
    /// Search execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }
}

/// <summary>
/// Information about the search query
/// </summary>
public class QueryInfo
{
    /// <summary>
    /// Query type: "failure_id" or "failure_text"
    /// </summary>
    public required string QueryType { get; set; }

    /// <summary>
    /// Failure ID if querying by existing failure
    /// </summary>
    public int? FailureId { get; set; }

    /// <summary>
    /// Error type of the query failure
    /// </summary>
    public string? ErrorType { get; set; }

    /// <summary>
    /// Minimum similarity threshold applied
    /// </summary>
    public float MinSimilarity { get; set; }

    /// <summary>
    /// Maximum results requested
    /// </summary>
    public int TopN { get; set; }
}

/// <summary>
/// A single similar failure result
/// </summary>
public class SimilarFailureResult
{
    /// <summary>
    /// Failure ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Error type
    /// </summary>
    public required string ErrorType { get; set; }

    /// <summary>
    /// Root cause summary
    /// </summary>
    public required string RootCause { get; set; }

    /// <summary>
    /// Detailed analysis
    /// </summary>
    public required string DetailedAnalysis { get; set; }

    /// <summary>
    /// When this failure was analyzed
    /// </summary>
    public DateTime AnalyzedAt { get; set; }

    /// <summary>
    /// Cosine similarity score (0.0 to 1.0)
    /// </summary>
    public float SimilarityScore { get; set; }

    /// <summary>
    /// Similarity as percentage (0-100)
    /// </summary>
    public float SimilarityPercentage => SimilarityScore * 100f;

    /// <summary>
    /// Confidence score of the original RCA
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Number of related commits (if any)
    /// </summary>
    public int CommitCount { get; set; }

    /// <summary>
    /// Repository analyzed (if any)
    /// </summary>
    public string? Repository { get; set; }
}
