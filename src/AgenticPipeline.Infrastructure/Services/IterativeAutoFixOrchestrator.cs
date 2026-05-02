using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Orchestrates iterative auto-fix sessions with retry logic on the same branch
/// </summary>
public class IterativeAutoFixOrchestrator
{
    private readonly IAutoFixService _autoFixService;
    private readonly AutoFixSessionManager _sessionManager;
    private readonly ILogger<IterativeAutoFixOrchestrator> _logger;

    public IterativeAutoFixOrchestrator(
        IAutoFixService autoFixService,
        AutoFixSessionManager sessionManager,
        ILogger<IterativeAutoFixOrchestrator> logger)
    {
        _autoFixService = autoFixService;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Starts a new auto-fix session (initial failure from master/main)
    /// </summary>
    public async Task<AutoFixSession?> StartNewSessionAsync(
        OrchestrationResult orchestrationResult,
        string owner,
        string repo,
        AutoFixSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if session already exists for this orchestration
            if (_sessionManager.HasActiveSessionForOrchestration(orchestrationResult.OrchestrationId))
            {
                var existingSession = _sessionManager.GetSessionByOrchestration(orchestrationResult.OrchestrationId);
                _logger.LogInformation(
                    "Session already exists for orchestration {OrchestrationId}: {SessionId} on branch {BranchName}. Skipping duplicate.",
                    orchestrationResult.OrchestrationId,
                    existingSession?.SessionId,
                    existingSession?.BranchName);
                return existingSession;
            }

            // Check if ANY session is waiting for pipeline result for this repo
            if (_sessionManager.HasWaitingSessionForRepo(owner, repo))
            {
                var waitingSession = _sessionManager.GetWaitingSessionForRepo(owner, repo);
                _logger.LogWarning(
                    "Another session {SessionId} on branch {BranchName} is waiting for pipeline result. Cannot start new session until it completes.",
                    waitingSession?.SessionId,
                    waitingSession?.BranchName);
                return null;
            }

            _logger.LogInformation(
                "Starting new auto-fix session for orchestration {OrchestrationId}",
                orchestrationResult.OrchestrationId);

            // Create session
            var session = _sessionManager.CreateSession(
                orchestrationResult.OrchestrationId,
                owner,
                repo,
                settings.MaxBranchRetries);

            // Generate fix with validation
            var autoFix = await _autoFixService.GenerateFixWithRefinementAsync(
                orchestrationResult,
                owner,
                repo,
                settings,
                cancellationToken);

            if (!autoFix.FixGenerated || !autoFix.IsSafeToApply || !autoFix.ValidationPassed)
            {
                _logger.LogWarning(
                    "Failed to generate valid fix for session {SessionId}. Reason: {Reason}",
                    session.SessionId,
                    autoFix.Reason ?? "Fix not generated or validation failed");

                _sessionManager.MarkFailed(session.BranchName, autoFix.Reason ?? "Fix generation failed");
                return null;
            }

            // Create branch and commit fixes WITHOUT creating PR
            // PR will be created later when pipeline succeeds
            var commitSha = await _autoFixService.CreateBranchAndCommitAsync(
                autoFix,
                owner,
                repo,
                session.BranchName,
                baseBranch: null,
                cancellationToken);

            if (string.IsNullOrEmpty(commitSha))
            {
                _logger.LogWarning(
                    "Failed to create branch and commit for session {SessionId}",
                    session.SessionId);

                _sessionManager.MarkFailed(session.BranchName, "Failed to create branch");
                return null;
            }

            // Record this attempt
            _sessionManager.RecordFailure(
                session.BranchName,
                orchestrationResult.Failure?.ErrorType ?? "Unknown",
                orchestrationResult.Failure?.RootCause ?? "Unknown",
                autoFix.FixDescription ?? "Unknown fix",
                commitSha: commitSha,
                filesModified: autoFix.FileChanges.Select(fc => fc.FilePath).ToList());

            // Mark session as waiting for pipeline result
            _sessionManager.MarkWaitingForPipeline(session.BranchName);

            _logger.LogInformation(
                "Session {SessionId} started on branch {BranchName}. Commit {CommitSha}. Marked as WAITING FOR PIPELINE - no further action until pipeline completes.",
                session.SessionId,
                session.BranchName,
                commitSha);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting new auto-fix session");
            return null;
        }
    }

    /// <summary>
    /// Handles retry when an autofix branch build fails
    /// </summary>
    public async Task<bool> RetryOnExistingBranchAsync(
        string branchName,
        OrchestrationResult newOrchestrationResult,
        string owner,
        string repo,
        AutoFixSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = _sessionManager.GetSessionByBranch(branchName);
            if (session == null)
            {
                _logger.LogWarning("No active session found for branch {BranchName}", branchName);
                return false;
            }

            // Session SHOULD be waiting - that's the correct state after committing a fix
            // The webhook handler already verified this before calling us
            if (!session.IsWaitingForPipeline)
            {
                _logger.LogWarning(
                    "Session {SessionId} is NOT waiting for pipeline. This might be a duplicate or out-of-order webhook.",
                    session.SessionId);
                return false;
            }

            if (!session.CanRetry)
            {
                _logger.LogWarning(
                    "Session {SessionId} cannot retry (attempt {Attempt}/{MaxRetries}, status: {Status})",
                    session.SessionId,
                    session.RetryAttempt,
                    session.MaxRetries,
                    session.Status);

                return false;
            }

            _logger.LogInformation(
                "Retrying auto-fix for session {SessionId}, attempt {Attempt}/{MaxRetries}",
                session.SessionId,
                session.RetryAttempt + 1,
                session.MaxRetries);

            // Mark pipeline as completed (so we can process this failure)
            _sessionManager.MarkPipelineCompleted(branchName);

            // Build detailed failure reason from the NEW error
            var whyItFailed = BuildDetailedFailureReason(newOrchestrationResult.Failure);
            _sessionManager.UpdateLastFailureReason(branchName, whyItFailed);

            // Build accumulated context from all previous failures
            var accumulatedContext = _sessionManager.BuildAccumulatedContext(branchName);

            // Add NEW failure analysis and accumulated context to orchestration result
            if (newOrchestrationResult.Failure != null)
            {
                var enhancedAnalysis = new StringBuilder();
                enhancedAnalysis.AppendLine("## NEW FAILURE AFTER PREVIOUS FIX ATTEMPT");
                enhancedAnalysis.AppendLine();
                enhancedAnalysis.AppendLine($"**Error Type:** {newOrchestrationResult.Failure.ErrorType}");
                enhancedAnalysis.AppendLine($"**Root Cause:** {newOrchestrationResult.Failure.RootCause}");
                enhancedAnalysis.AppendLine($"**Confidence:** {newOrchestrationResult.Failure.ConfidenceScore:P0}");
                enhancedAnalysis.AppendLine();
                enhancedAnalysis.AppendLine("**Detailed Analysis of New Failure:**");
                enhancedAnalysis.AppendLine(newOrchestrationResult.Failure.DetailedAnalysis);
                enhancedAnalysis.AppendLine();
                enhancedAnalysis.AppendLine("---");
                enhancedAnalysis.AppendLine();
                enhancedAnalysis.AppendLine(accumulatedContext);
                enhancedAnalysis.AppendLine();
                enhancedAnalysis.AppendLine("## CRITICAL INSTRUCTIONS FOR RETRY:");
                enhancedAnalysis.AppendLine("1. The previous fix did NOT work - analyze the NEW error above carefully");
                enhancedAnalysis.AppendLine("2. Compare the NEW error with previous errors - is it the same or different?");
                enhancedAnalysis.AppendLine("3. If SAME error: The previous fix didn't address the root cause - try a completely different approach");
                enhancedAnalysis.AppendLine("4. If DIFFERENT error: The previous fix may have created a new problem - fix the new issue");
                enhancedAnalysis.AppendLine("5. DO NOT repeat the exact same fix that already failed");
                enhancedAnalysis.AppendLine("6. Consider if multiple files need to be changed together");
                enhancedAnalysis.AppendLine("7. Think about dependencies, side effects, and related code");

                newOrchestrationResult.Failure.DetailedAnalysis = enhancedAnalysis.ToString();
            }

            // Generate new fix with accumulated context
            var autoFix = await _autoFixService.GenerateFixWithRefinementAsync(
                newOrchestrationResult,
                owner,
                repo,
                settings,
                cancellationToken);

            if (!autoFix.FixGenerated || !autoFix.IsSafeToApply || !autoFix.ValidationPassed)
            {
                _logger.LogWarning(
                    "Failed to generate valid fix on retry for session {SessionId}. Reason: {Reason}",
                    session.SessionId,
                    autoFix.Reason ?? "Fix not generated or validation failed");

                _sessionManager.IncrementRetry(branchName);

                if (!session.CanRetry)
                {
                    _sessionManager.MarkFailed(branchName, "Max retries reached without successful fix");
                }

                return false;
            }

            // Increment retry counter
            _sessionManager.IncrementRetry(branchName);

            // Commit to EXISTING branch (not create new)
            var commitSha = await _autoFixService.CommitToExistingBranchAsync(
                autoFix,
                owner,
                repo,
                branchName,
                session.RetryAttempt,
                cancellationToken);

            if (string.IsNullOrEmpty(commitSha))
            {
                _logger.LogWarning(
                    "Failed to commit retry fix to branch {BranchName}",
                    branchName);

                return false;
            }

            // Record this attempt
            _sessionManager.RecordFailure(
                branchName,
                newOrchestrationResult.Failure?.ErrorType ?? "Unknown",
                newOrchestrationResult.Failure?.RootCause ?? "Unknown",
                autoFix.FixDescription ?? "Unknown fix",
                commitSha: commitSha,
                filesModified: autoFix.FileChanges.Select(fc => fc.FilePath).ToList());

            // Mark session as waiting for pipeline result again
            _sessionManager.MarkWaitingForPipeline(branchName);

            _logger.LogInformation(
                "Session {SessionId} retry committed to branch {BranchName}. Commit: {CommitSha}. Marked as WAITING FOR PIPELINE - no further action until pipeline completes.",
                session.SessionId,
                branchName,
                commitSha);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying auto-fix on branch {BranchName}", branchName);
            return false;
        }
    }

    /// <summary>
    /// Handles successful pipeline run on autofix branch (creates PR)
    /// </summary>
    public async Task<int?> HandleSuccessfulPipelineAsync(
        string branchName,
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = _sessionManager.GetSessionByBranch(branchName);
            if (session == null)
            {
                _logger.LogWarning("No active session found for successful branch {BranchName}", branchName);
                return null;
            }

            // Check if session is waiting for pipeline (should be true)
            if (!session.IsWaitingForPipeline)
            {
                _logger.LogWarning(
                    "Session {SessionId} is not waiting for pipeline. Possible duplicate success webhook.",
                    session.SessionId);
            }

            // Mark pipeline as completed
            _sessionManager.MarkPipelineCompleted(branchName);

            _logger.LogInformation(
                "Pipeline succeeded for session {SessionId} on branch {BranchName}. Creating PR...",
                session.SessionId,
                branchName);

            // Build PR body with session history
            var prBody = BuildPRBodyFromSession(session);
            var prTitle = $"[Auto-Fix] Fix applied after {session.RetryAttempt} attempt(s)";

            // Create PR
            var prNumber = await _autoFixService.CreatePRForBranchAsync(
                branchName,
                owner,
                repo,
                prTitle,
                prBody,
                baseBranch: null,
                cancellationToken);

            if (prNumber.HasValue)
            {
                _sessionManager.MarkSucceeded(branchName, prNumber.Value);

                _logger.LogInformation(
                    "Session {SessionId} completed successfully. PR #{PrNumber} created after {Attempts} attempt(s)",
                    session.SessionId,
                    prNumber.Value,
                    session.RetryAttempt);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to create PR for successful session {SessionId}",
                    session.SessionId);
            }

            return prNumber;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating PR for successful branch {BranchName}", branchName);
            return null;
        }
    }

    private string BuildDetailedFailureReason(FailureEntity? failure)
    {
        if (failure == null)
            return "Unknown failure";

        var reason = new StringBuilder();
        reason.AppendLine($"**Error Type:** {failure.ErrorType}");
        reason.AppendLine($"**Root Cause:** {failure.RootCause}");
        reason.AppendLine($"**Confidence:** {failure.ConfidenceScore:P0}");

        if (!string.IsNullOrEmpty(failure.DetailedAnalysis))
        {
            reason.AppendLine();
            reason.AppendLine("**Analysis:**");
            // Take first 500 characters to avoid too much data
            var analysis = failure.DetailedAnalysis.Length > 500
                ? failure.DetailedAnalysis.Substring(0, 500) + "..."
                : failure.DetailedAnalysis;
            reason.AppendLine(analysis);
        }

        return reason.ToString();
    }

    private string BuildPRBodyFromSession(AutoFixSession session)
    {
        var body = new StringBuilder();
        body.AppendLine("## Auto-Fix Session Summary");
        body.AppendLine();
        body.AppendLine($"**Session ID:** {session.SessionId}");
        body.AppendLine($"**Branch:** {session.BranchName}");
        body.AppendLine($"**Total Attempts:** {session.RetryAttempt}");
        body.AppendLine($"**Session Duration:** {(session.LastActivityAt - session.SessionStarted).TotalMinutes:F1} minutes");
        body.AppendLine();

        if (session.FailureHistory.Any())
        {
            body.AppendLine("## Fix Attempts");
            body.AppendLine();

            foreach (var history in session.FailureHistory)
            {
                body.AppendLine($"### Attempt {history.AttemptNumber}");
                body.AppendLine($"**Error Type:** {history.ErrorType}");
                body.AppendLine($"**Root Cause:** {history.RootCause}");
                body.AppendLine($"**Fix Applied:** {history.FixAttempted}");

                if (!string.IsNullOrEmpty(history.WhyItFailed) && history.AttemptNumber < session.FailureHistory.Count)
                {
                    body.AppendLine($"**Why It Failed:** {history.WhyItFailed}");
                }

                if (history.FilesModified.Any())
                {
                    body.AppendLine($"**Files Modified:** {string.Join(", ", history.FilesModified)}");
                }

                if (!string.IsNullOrEmpty(history.CommitSha))
                {
                    body.AppendLine($"**Commit:** {history.CommitSha.Substring(0, Math.Min(7, history.CommitSha.Length))}");
                }

                body.AppendLine();
            }
        }

        body.AppendLine("---");
        body.AppendLine("*This PR was automatically generated after iterative fix attempts.*");
        body.AppendLine();
        body.AppendLine("The pipeline has succeeded with this fix. Please review the changes before merging.");

        return body.ToString();
    }
}
