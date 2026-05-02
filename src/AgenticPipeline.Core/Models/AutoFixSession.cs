namespace AgenticPipeline.Core.Models;

/// <summary>
/// Represents an active auto-fix session tracking retries on a single branch
/// </summary>
public class AutoFixSession
{
    /// <summary>
    /// Unique session ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Branch name for this session (e.g., autofix/session-20260427-123456)
    /// </summary>
    public string BranchName { get; set; } = string.Empty;

    /// <summary>
    /// Original orchestration ID that started this session
    /// </summary>
    public Guid OriginalOrchestrationId { get; set; }

    /// <summary>
    /// Repository owner
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Repository name
    /// </summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>
    /// Current retry attempt number (1-based)
    /// </summary>
    public int RetryAttempt { get; set; } = 1;

    /// <summary>
    /// Maximum retries allowed
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// When this session started
    /// </summary>
    public DateTime SessionStarted { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current status of the session
    /// </summary>
    public AutoFixSessionStatus Status { get; set; } = AutoFixSessionStatus.InProgress;

    /// <summary>
    /// Is the session waiting for pipeline to complete?
    /// </summary>
    public bool IsWaitingForPipeline { get; set; } = false;

    /// <summary>
    /// Last pipeline run ID being tracked
    /// </summary>
    public long? LastPipelineRunId { get; set; }

    /// <summary>
    /// History of all failures in this session
    /// </summary>
    public List<SessionFailureHistory> FailureHistory { get; set; } = new();

    /// <summary>
    /// PR number if created
    /// </summary>
    public int? PrNumber { get; set; }

    /// <summary>
    /// Accumulated context from all attempts
    /// </summary>
    public string AccumulatedContext { get; set; } = string.Empty;

    /// <summary>
    /// Check if session can retry
    /// </summary>
    public bool CanRetry => RetryAttempt < MaxRetries && Status == AutoFixSessionStatus.InProgress;
}

/// <summary>
/// Status of an auto-fix session
/// </summary>
public enum AutoFixSessionStatus
{
    InProgress,
    Succeeded,
    Failed,
    MaxRetriesReached,
    Abandoned
}

/// <summary>
/// Records a single failure in the session
/// </summary>
public class SessionFailureHistory
{
    /// <summary>
    /// Attempt number
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// When this attempt was made
    /// </summary>
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Error type from the failure
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// Root cause identified
    /// </summary>
    public string RootCause { get; set; } = string.Empty;

    /// <summary>
    /// What fix was attempted
    /// </summary>
    public string FixAttempted { get; set; } = string.Empty;

    /// <summary>
    /// Why the fix failed (from next pipeline run)
    /// </summary>
    public string? WhyItFailed { get; set; }

    /// <summary>
    /// Commit SHA for this attempt
    /// </summary>
    public string? CommitSha { get; set; }

    /// <summary>
    /// Files modified in this attempt
    /// </summary>
    public List<string> FilesModified { get; set; } = new();
}
