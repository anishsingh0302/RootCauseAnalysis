namespace AgenticPipeline.Core.Models;

/// <summary>
/// Result of an auto-fix attempt
/// </summary>
public class AutoFixResult
{
    /// <summary>
    /// Whether a fix was successfully generated
    /// </summary>
    public bool FixGenerated { get; set; }

    /// <summary>
    /// Whether the fix is safe to apply automatically
    /// </summary>
    public bool IsSafeToApply { get; set; }

    /// <summary>
    /// Confidence level of the fix (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Description of what the fix does
    /// </summary>
    public string? FixDescription { get; set; }

    /// <summary>
    /// List of files to be modified with their changes
    /// </summary>
    public List<FileChange> FileChanges { get; set; } = new();

    /// <summary>
    /// Reason why fix was not generated or is not safe (if applicable)
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Branch name for the PR
    /// </summary>
    public string? BranchName { get; set; }

    /// <summary>
    /// PR title
    /// </summary>
    public string? PrTitle { get; set; }

    /// <summary>
    /// PR description/body
    /// </summary>
    public string? PrBody { get; set; }
}

/// <summary>
/// Represents a change to a file
/// </summary>
public class FileChange
{
    /// <summary>
    /// Path to the file relative to repository root
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Type of change (modify, create, delete)
    /// </summary>
    public FileChangeType ChangeType { get; set; }

    /// <summary>
    /// New content of the file (for create/modify) - use this OR LineChanges, not both
    /// </summary>
    public string? NewContent { get; set; }

    /// <summary>
    /// Line-based changes (more efficient for large files) - use this OR NewContent, not both
    /// </summary>
    public List<LineChange>? LineChanges { get; set; }

    /// <summary>
    /// Original content (for reference)
    /// </summary>
    public string? OriginalContent { get; set; }

    /// <summary>
    /// Explanation of why this change fixes the issue
    /// </summary>
    public string? ChangeReason { get; set; }
}

/// <summary>
/// Represents a line-based change in a file (more efficient than full content)
/// </summary>
public class LineChange
{
    /// <summary>
    /// Line number to change (1-based indexing)
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Original line content (for verification)
    /// </summary>
    public string? OriginalLine { get; set; }

    /// <summary>
    /// New line content (null to delete line)
    /// </summary>
    public string? NewLine { get; set; }

    /// <summary>
    /// Type of change: "replace", "insert", "delete"
    /// </summary>
    public string ChangeType { get; set; } = "replace";
}

/// <summary>
/// Type of file change
/// </summary>
public enum FileChangeType
{
    Modify,
    Create,
    Delete
}
