namespace AgenticPipeline.Core.Models;

/// <summary>
/// Database entity for storing pipeline failure analyses
/// </summary>
public class FailureEntity
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Original log content that was analyzed
    /// </summary>
    public string LogContent { get; set; } = string.Empty;

    /// <summary>
    /// Brief summary of the root cause
    /// </summary>
    public string RootCause { get; set; } = string.Empty;

    /// <summary>
    /// Detailed analysis of the failure
    /// </summary>
    public string DetailedAnalysis { get; set; } = string.Empty;

    /// <summary>
    /// Type of error (e.g., CompilationError, TestFailure, DependencyIssue)
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score of the analysis (0.0 to 1.0)
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// When this analysis was performed
    /// </summary>
    public DateTime AnalyzedAt { get; set; }

    /// <summary>
    /// Affected components/files as JSON array
    /// </summary>
    public string AffectedComponentsJson { get; set; } = string.Empty;

    /// <summary>
    /// Recommendations as JSON array
    /// </summary>
    public string RecommendationsJson { get; set; } = string.Empty;

    /// <summary>
    /// Input tokens used for this analysis
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// Output tokens generated for this analysis
    /// </summary>
    public int OutputTokens { get; set; }

    /// <summary>
    /// Vector embedding for similarity search (optional, for Phase 3)
    /// Stored as byte array (serialized float[])
    /// </summary>
    public byte[]? Embedding { get; set; }

    /// <summary>
    /// Dimension of the embedding vector (e.g., 1536 for Titan)
    /// </summary>
    public int? EmbeddingDimension { get; set; }

    /// <summary>
    /// Suspected commits as JSON (optional, from Phase 2)
    /// </summary>
    public string? SuspectedCommitsJson { get; set; }

    /// <summary>
    /// GitHub repository analyzed (owner/repo format, optional)
    /// </summary>
    public string? Repository { get; set; }

    /// <summary>
    /// Total commits analyzed for this failure (optional)
    /// </summary>
    public int? CommitsAnalyzed { get; set; }

    /// <summary>
    /// Navigation property - related commits
    /// </summary>
    public ICollection<CommitEntity> Commits { get; set; } = new List<CommitEntity>();
}
