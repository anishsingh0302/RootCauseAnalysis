using AgenticPipeline.Core.Models;

namespace AgenticPipeline.Core.Interfaces;

/// <summary>
/// Service for generating automatic fixes for failures
/// </summary>
public interface IAutoFixService
{
    /// <summary>
    /// Attempts to generate an automatic fix for a failure
    /// </summary>
    /// <param name="orchestrationResult">The orchestration result containing failure analysis</param>
    /// <param name="owner">GitHub repository owner</param>
    /// <param name="repo">GitHub repository name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Auto-fix result indicating if a fix was generated and is safe to apply</returns>
    Task<AutoFixResult> GenerateFixAsync(
        OrchestrationResult orchestrationResult,
        string owner,
        string repo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the fix by creating a branch, committing changes, and opening a PR
    /// </summary>
    /// <param name="autoFixResult">The fix to apply</param>
    /// <param name="owner">GitHub repository owner</param>
    /// <param name="repo">GitHub repository name</param>
    /// <param name="baseBranch">Base branch to create PR against (default: main/master)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PR number if successful</returns>
    Task<int?> ApplyFixAndCreatePRAsync(
        AutoFixResult autoFixResult,
        string owner,
        string repo,
        string? baseBranch = null,
        CancellationToken cancellationToken = default);
}
