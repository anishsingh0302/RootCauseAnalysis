using AgenticPipeline.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Manages active auto-fix sessions for iterative retry logic
/// </summary>
public class AutoFixSessionManager
{
    private readonly ConcurrentDictionary<string, AutoFixSession> _activeSessions = new();
    private readonly ILogger<AutoFixSessionManager> _logger;

    public AutoFixSessionManager(ILogger<AutoFixSessionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a new auto-fix session
    /// </summary>
    public AutoFixSession CreateSession(
        Guid orchestrationId,
        string owner,
        string repo,
        int maxRetries = 3)
    {
        var sessionId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var branchName = $"autofix/session-{timestamp}";

        var session = new AutoFixSession
        {
            SessionId = sessionId,
            BranchName = branchName,
            OriginalOrchestrationId = orchestrationId,
            Owner = owner,
            Repo = repo,
            MaxRetries = maxRetries,
            SessionStarted = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            Status = AutoFixSessionStatus.InProgress,
            RetryAttempt = 1
        };

        _activeSessions[branchName] = session;
        _logger.LogInformation(
            "Created auto-fix session {SessionId} on branch {BranchName}",
            sessionId, branchName);

        return session;
    }

    /// <summary>
    /// Gets session by branch name
    /// </summary>
    public AutoFixSession? GetSessionByBranch(string branchName)
    {
        _activeSessions.TryGetValue(branchName, out var session);
        return session;
    }

    /// <summary>
    /// Checks if a branch is an active autofix branch
    /// </summary>
    public bool IsAutoFixBranch(string branchName)
    {
        return branchName.StartsWith("autofix/session-") && _activeSessions.ContainsKey(branchName);
    }

    /// <summary>
    /// Checks if there's already an active session for this orchestration
    /// </summary>
    public bool HasActiveSessionForOrchestration(Guid orchestrationId)
    {
        return _activeSessions.Values.Any(s =>
            s.OriginalOrchestrationId == orchestrationId &&
            s.Status == AutoFixSessionStatus.InProgress);
    }

    /// <summary>
    /// Gets active session by orchestration ID
    /// </summary>
    public AutoFixSession? GetSessionByOrchestration(Guid orchestrationId)
    {
        return _activeSessions.Values.FirstOrDefault(s =>
            s.OriginalOrchestrationId == orchestrationId &&
            s.Status == AutoFixSessionStatus.InProgress);
    }

    /// <summary>
    /// Checks if there's any session waiting for pipeline result for this repo
    /// </summary>
    public bool HasWaitingSessionForRepo(string owner, string repo)
    {
        return _activeSessions.Values.Any(s =>
            s.Owner == owner &&
            s.Repo == repo &&
            s.IsWaitingForPipeline &&
            s.Status == AutoFixSessionStatus.InProgress);
    }

    /// <summary>
    /// Gets the waiting session for a repository
    /// </summary>
    public AutoFixSession? GetWaitingSessionForRepo(string owner, string repo)
    {
        return _activeSessions.Values.FirstOrDefault(s =>
            s.Owner == owner &&
            s.Repo == repo &&
            s.IsWaitingForPipeline &&
            s.Status == AutoFixSessionStatus.InProgress);
    }

    /// <summary>
    /// Marks session as waiting for pipeline result
    /// </summary>
    public void MarkWaitingForPipeline(string branchName, long? pipelineRunId = null)
    {
        if (_activeSessions.TryGetValue(branchName, out var session))
        {
            session.IsWaitingForPipeline = true;
            session.LastPipelineRunId = pipelineRunId;
            session.LastActivityAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Session {SessionId} marked as waiting for pipeline. RunId: {RunId}",
                session.SessionId,
                pipelineRunId);
        }
    }

    /// <summary>
    /// Marks session as ready to process (pipeline completed)
    /// </summary>
    public void MarkPipelineCompleted(string branchName)
    {
        if (_activeSessions.TryGetValue(branchName, out var session))
        {
            session.IsWaitingForPipeline = false;
            session.LastActivityAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Session {SessionId} pipeline completed. Ready for next action.",
                session.SessionId);
        }
    }

    /// <summary>
    /// Records a failure in the session
    /// </summary>
    public void RecordFailure(
        string branchName,
        string errorType,
        string rootCause,
        string fixAttempted,
        string? commitSha = null,
        List<string>? filesModified = null)
    {
        if (_activeSessions.TryGetValue(branchName, out var session))
        {
            var history = new SessionFailureHistory
            {
                AttemptNumber = session.RetryAttempt,
                AttemptedAt = DateTime.UtcNow,
                ErrorType = errorType,
                RootCause = rootCause,
                FixAttempted = fixAttempted,
                CommitSha = commitSha,
                FilesModified = filesModified ?? new List<string>()
            };

            session.FailureHistory.Add(history);
            session.LastActivityAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Recorded failure for session {SessionId}, attempt {Attempt}/{MaxRetries}",
                session.SessionId, session.RetryAttempt, session.MaxRetries);
        }
    }

    /// <summary>
    /// Updates why the last fix failed (from new pipeline failure)
    /// </summary>
    public void UpdateLastFailureReason(string branchName, string whyItFailed)
    {
        if (_activeSessions.TryGetValue(branchName, out var session))
        {
            var lastFailure = session.FailureHistory.LastOrDefault();
            if (lastFailure != null)
            {
                lastFailure.WhyItFailed = whyItFailed;
                _logger.LogInformation(
                    "Updated failure reason for session {SessionId}, attempt {Attempt}",
                    session.SessionId, lastFailure.AttemptNumber);
            }
        }
    }

    /// <summary>
    /// Increments retry attempt counter
    /// </summary>
    public void IncrementRetry(string branchName)
    {
        if (_activeSessions.TryGetValue(branchName, out var session))
        {
            session.RetryAttempt++;
            session.LastActivityAt = DateTime.UtcNow;

            if (session.RetryAttempt >= session.MaxRetries)
            {
                session.Status = AutoFixSessionStatus.MaxRetriesReached;
                _logger.LogWarning(
                    "Session {SessionId} reached max retries ({MaxRetries})",
                    session.SessionId, session.MaxRetries);
            }
        }
    }

    /// <summary>
    /// Builds accumulated context from all previous failures
    /// </summary>
    public string BuildAccumulatedContext(string branchName)
    {
        if (!_activeSessions.TryGetValue(branchName, out var session))
        {
            return string.Empty;
        }

        var context = new System.Text.StringBuilder();
        context.AppendLine($"## Auto-Fix Session History");
        context.AppendLine($"Session ID: {session.SessionId}");
        context.AppendLine($"Branch: {session.BranchName}");
        context.AppendLine($"Current Attempt: {session.RetryAttempt}/{session.MaxRetries}");
        context.AppendLine();

        if (session.FailureHistory.Count > 0)
        {
            context.AppendLine("## Previous Fix Attempts and Failures:");
            context.AppendLine();

            for (int i = 0; i < session.FailureHistory.Count; i++)
            {
                var history = session.FailureHistory[i];
                context.AppendLine($"### Attempt {history.AttemptNumber}");
                context.AppendLine($"**Error Type:** {history.ErrorType}");
                context.AppendLine($"**Root Cause:** {history.RootCause}");
                context.AppendLine($"**Fix Attempted:** {history.FixAttempted}");

                if (!string.IsNullOrEmpty(history.WhyItFailed))
                {
                    context.AppendLine($"**Why It Failed:** {history.WhyItFailed}");
                }

                if (history.FilesModified.Any())
                {
                    context.AppendLine($"**Files Modified:** {string.Join(", ", history.FilesModified)}");
                }

                context.AppendLine();
            }

            context.AppendLine("## CRITICAL INSTRUCTIONS:");
            context.AppendLine("- DO NOT repeat the same fix that already failed");
            context.AppendLine("- Analyze WHY previous fixes failed and try a different approach");
            context.AppendLine("- Consider if the problem is in a different file than previously fixed");
            context.AppendLine("- Check if multiple issues need to be fixed together");
            context.AppendLine();
        }

        session.AccumulatedContext = context.ToString();
        return session.AccumulatedContext;
    }

    /// <summary>
    /// Marks session as succeeded
    /// </summary>
    public void MarkSucceeded(string branchName, int prNumber)
    {
        if (_activeSessions.TryGetValue(branchName, out var session))
        {
            session.Status = AutoFixSessionStatus.Succeeded;
            session.PrNumber = prNumber;
            session.LastActivityAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Session {SessionId} succeeded on attempt {Attempt}, PR #{PrNumber} created",
                session.SessionId, session.RetryAttempt, prNumber);
        }
    }

    /// <summary>
    /// Marks session as failed
    /// </summary>
    public void MarkFailed(string branchName, string reason)
    {
        if (_activeSessions.TryGetValue(branchName, out var session))
        {
            session.Status = AutoFixSessionStatus.Failed;
            session.LastActivityAt = DateTime.UtcNow;

            _logger.LogWarning(
                "Session {SessionId} failed: {Reason}",
                session.SessionId, reason);
        }
    }

    /// <summary>
    /// Removes old sessions (cleanup)
    /// </summary>
    public void CleanupOldSessions(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var oldSessions = _activeSessions
            .Where(kvp => kvp.Value.LastActivityAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var branchName in oldSessions)
        {
            if (_activeSessions.TryRemove(branchName, out var session))
            {
                _logger.LogInformation(
                    "Cleaned up old session {SessionId} (last activity: {LastActivity})",
                    session.SessionId, session.LastActivityAt);
            }
        }
    }

    /// <summary>
    /// Gets all active sessions
    /// </summary>
    public List<AutoFixSession> GetActiveSessions()
    {
        return _activeSessions.Values.ToList();
    }
}
