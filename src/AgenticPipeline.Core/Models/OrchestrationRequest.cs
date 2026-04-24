namespace AgenticPipeline.Core.Models;

/// <summary>
/// Request to orchestrate a full pipeline analysis
/// </summary>
public class OrchestrationRequest
{
    /// <summary>
    /// The failure log content to analyze
    /// </summary>
    public string LogContent { get; set; } = string.Empty;

    /// <summary>
    /// Optional: GitHub repository owner (for commit analysis)
    /// </summary>
    public string? GitHubOwner { get; set; }

    /// <summary>
    /// Optional: GitHub repository name (for commit analysis)
    /// </summary>
    public string? GitHubRepo { get; set; }

    /// <summary>
    /// Optional: Failure timestamp (for commit correlation)
    /// </summary>
    public DateTime? FailureTimestamp { get; set; }

    /// <summary>
    /// Optional: Number of days to look back for commits (default: 7)
    /// </summary>
    public int CommitLookbackDays { get; set; } = 7;

    /// <summary>
    /// Optional: Number of similar failures to retrieve (default: 5)
    /// </summary>
    public int SimilarFailuresCount { get; set; } = 5;

    /// <summary>
    /// Optional: Minimum similarity threshold (default: 0.7)
    /// </summary>
    public float MinSimilarity { get; set; } = 0.7f;

    /// <summary>
    /// Whether to attempt pattern detection and assignment (default: true)
    /// </summary>
    public bool EnablePatternDetection { get; set; } = true;

    /// <summary>
    /// Whether to analyze related commits (default: true if GitHub info provided)
    /// </summary>
    public bool EnableCommitAnalysis { get; set; } = true;

    /// <summary>
    /// Optional: Source of the failure (e.g., "GitHub Actions", "Azure DevOps", "Manual")
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Optional: Additional context or metadata
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
