namespace AgenticPipeline.Core.Models;

/// <summary>
/// Configuration settings for auto-fix behavior
/// </summary>
public class AutoFixSettings
{
    /// <summary>
    /// Whether to enable validation before committing fixes
    /// </summary>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    /// Minimum confidence required to commit a fix (0.0-1.0)
    /// </summary>
    public double MinConfidenceForCommit { get; set; } = 0.9;

    /// <summary>
    /// Maximum number of refinement attempts when fix validation fails
    /// </summary>
    public int MaxFixRefinementAttempts { get; set; } = 3;

    /// <summary>
    /// Whether to enable iterative retry on autofix branch failures
    /// </summary>
    public bool EnableIterativeRetry { get; set; } = true;

    /// <summary>
    /// Maximum number of retries on an autofix branch before giving up
    /// </summary>
    public int MaxBranchRetries { get; set; } = 3;

    /// <summary>
    /// Whether to require explicit error matching between analysis and fix
    /// </summary>
    public bool RequireExplicitErrorMatch { get; set; } = true;

    /// <summary>
    /// Whether to enable impact analysis before committing
    /// </summary>
    public bool EnableImpactAnalysis { get; set; } = true;
}
