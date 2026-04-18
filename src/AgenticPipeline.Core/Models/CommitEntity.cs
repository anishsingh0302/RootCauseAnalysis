namespace AgenticPipeline.Core.Models;

/// <summary>
/// Database entity for storing GitHub commit information related to failures
/// </summary>
public class CommitEntity
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Full commit SHA
    /// </summary>
    public string Sha { get; set; } = string.Empty;

    /// <summary>
    /// Commit author name
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Commit author email
    /// </summary>
    public string AuthorEmail { get; set; } = string.Empty;

    /// <summary>
    /// When the commit was made
    /// </summary>
    public DateTime CommitDate { get; set; }

    /// <summary>
    /// Commit message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Files changed in this commit as JSON array
    /// </summary>
    public string FilesChangedJson { get; set; } = string.Empty;

    /// <summary>
    /// Type of change (e.g., "added", "modified", "removed")
    /// </summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>
    /// Number of lines added
    /// </summary>
    public int Additions { get; set; }

    /// <summary>
    /// Number of lines deleted
    /// </summary>
    public int Deletions { get; set; }

    /// <summary>
    /// Commit URL on GitHub
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score that this commit caused the failure (0.0 to 1.0, optional)
    /// </summary>
    public double? ConfidenceScore { get; set; }

    /// <summary>
    /// Reason why this commit is suspected (optional)
    /// </summary>
    public string? SuspectedReason { get; set; }

    /// <summary>
    /// Evidence from failure log linking to this commit (optional)
    /// </summary>
    public string? Evidence { get; set; }

    /// <summary>
    /// Foreign key to related failure
    /// </summary>
    public int? FailureEntityId { get; set; }

    /// <summary>
    /// Navigation property - related failure
    /// </summary>
    public FailureEntity? Failure { get; set; }
}
