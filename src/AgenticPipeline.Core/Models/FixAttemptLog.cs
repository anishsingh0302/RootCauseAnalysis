namespace AgenticPipeline.Core.Models;

/// <summary>
/// Logs a single attempt at generating and validating a fix
/// </summary>
public class FixAttemptLog
{
    /// <summary>
    /// Attempt number (1, 2, 3, etc.)
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Timestamp of this attempt
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Confidence from RCA analysis
    /// </summary>
    public double RcaConfidence { get; set; }

    /// <summary>
    /// Confidence from fix generation
    /// </summary>
    public double FixConfidence { get; set; }

    /// <summary>
    /// Confidence from validation
    /// </summary>
    public double ValidationConfidence { get; set; }

    /// <summary>
    /// Overall calculated confidence
    /// </summary>
    public double OverallConfidence { get; set; }

    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool ValidationPassed { get; set; }

    /// <summary>
    /// Feedback from validation
    /// </summary>
    public string ValidationFeedback { get; set; } = string.Empty;

    /// <summary>
    /// List of validation issues
    /// </summary>
    public List<string> ValidationIssues { get; set; } = new();

    /// <summary>
    /// Outcome: Committed, Rejected, Refined
    /// </summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>
    /// Additional notes
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}
