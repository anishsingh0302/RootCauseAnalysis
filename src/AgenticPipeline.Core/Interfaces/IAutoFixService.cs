using AgenticPipeline.Core.Models;

namespace AgenticPipeline.Core.Interfaces;

/// <summary>
/// Service for generating automatic fixes for failures
/// </summary>
public interface IAutoFixService
{
    /// <summary>
    /// Generates and validates a fix with iterative refinement
    /// </summary>
    /// <param name="orchestrationResult">The orchestration result containing failure analysis</param>
    /// <param name="owner">GitHub repository owner</param>
    /// <param name="repo">GitHub repository name</param>
    /// <param name="settings">Auto-fix settings for validation behavior</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Auto-fix result with validation results</returns>
    Task<AutoFixResult> GenerateFixWithRefinementAsync(
        OrchestrationResult orchestrationResult,
        string owner,
        string repo,
        AutoFixSettings? settings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to generate an automatic fix for a failure
    /// </summary>
    /// <param name="orchestrationResult">The orchestration result containing failure analysis</param>
    /// <param name="owner">GitHub repository owner</param>
    /// <param name="repo">GitHub repository name</param>
    /// <param name="errorAnalysis">Detailed error analysis if available</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Auto-fix result indicating if a fix was generated and is safe to apply</returns>
    Task<AutoFixResult> GenerateFixAsync(
        OrchestrationResult orchestrationResult,
        string owner,
        string repo,
        ErrorAnalysis? errorAnalysis = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the fix by creating a branch, committing changes, and opening a PR
    /// Only applies if validation passed and confidence is above threshold
    /// </summary>
    /// <param name="autoFixResult">The fix to apply</param>
    /// <param name="owner">GitHub repository owner</param>
    /// <param name="repo">GitHub repository name</param>
    /// <param name="baseBranch">Base branch to create PR against (default: main/master)</param>
    /// <param name="minConfidence">Minimum confidence required to proceed (default: 0.9)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PR number if successful</returns>
    Task<int?> ApplyFixAndCreatePRAsync(
        AutoFixResult autoFixResult,
        string owner,
        string repo,
        string? baseBranch = null,
        double minConfidence = 0.9,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits additional fixes to an existing branch (for iterative retry)
    /// </summary>
    /// <param name="autoFixResult">The fix to apply</param>
    /// <param name="owner">GitHub repository owner</param>
    /// <param name="repo">GitHub repository name</param>
    /// <param name="existingBranch">Existing branch to commit to</param>
    /// <param name="attemptNumber">Current attempt number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Commit SHA if successful</returns>
    Task<string?> CommitToExistingBranchAsync(
        AutoFixResult autoFixResult,
        string owner,
        string repo,
        string existingBranch,
        int attemptNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a PR for an existing branch (after pipeline succeeds)
    /// </summary>
    /// <param name="branchName">Branch to create PR from</param>
    /// <param name="owner">GitHub repository owner</param>
    /// <param name="repo">GitHub repository name</param>
    /// <param name="title">PR title</param>
    /// <param name="body">PR body</param>
    /// <param name="baseBranch">Base branch (default: main/master)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PR number if successful</returns>
    Task<int?> CreatePRForBranchAsync(
        string branchName,
        string owner,
        string repo,
        string title,
        string body,
        string? baseBranch = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new branch and commits fixes WITHOUT creating a PR
    /// Used for iterative retry - PR created later after pipeline succeeds
    /// </summary>
    /// <param name="autoFixResult">The fix to apply</param>
    /// <param name="owner">GitHub repository owner</param>
    /// <param name="repo">GitHub repository name</param>
    /// <param name="branchName">Branch name to create</param>
    /// <param name="baseBranch">Base branch to branch from (default: main/master)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Last commit SHA if successful</returns>
    Task<string?> CreateBranchAndCommitAsync(
        AutoFixResult autoFixResult,
        string owner,
        string repo,
        string branchName,
        string? baseBranch = null,
        CancellationToken cancellationToken = default);
}
