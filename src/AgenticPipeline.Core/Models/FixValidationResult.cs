namespace AgenticPipeline.Core.Models;

/// <summary>
/// Result of validating a proposed fix before applying it
/// </summary>
public class FixValidationResult
{
    /// <summary>
    /// Whether the fix is valid and safe to apply
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Confidence in validation (0.0-1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Whether the fix addresses the root cause
    /// </summary>
    public bool AddressesRootCause { get; set; }

    /// <summary>
    /// Whether the fix could break other code
    /// </summary>
    public bool CouldBreakOtherCode { get; set; }

    /// <summary>
    /// List of validation issues found
    /// </summary>
    public List<string> ValidationIssues { get; set; } = new();

    /// <summary>
    /// Recommendation: Proceed, Refine, or Reject
    /// </summary>
    public string Recommendation { get; set; } = string.Empty;

    /// <summary>
    /// Detailed reasoning for the validation result
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Whether the fix is complete (not just hiding symptom)
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Whether the fix is minimal (no unnecessary changes)
    /// </summary>
    public bool IsMinimal { get; set; }
}
