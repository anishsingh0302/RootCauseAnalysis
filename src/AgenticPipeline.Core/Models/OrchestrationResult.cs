using AgenticPipeline.Core.Interfaces;

namespace AgenticPipeline.Core.Models;

/// <summary>
/// Result of a complete pipeline orchestration
/// </summary>
public class OrchestrationResult
{
    /// <summary>
    /// Unique identifier for this orchestration run
    /// </summary>
    public Guid OrchestrationId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Timestamp when orchestration started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when orchestration completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration of the orchestration
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;

    /// <summary>
    /// Overall status of the orchestration
    /// </summary>
    public OrchestrationStatus Status { get; set; } = OrchestrationStatus.Running;

    /// <summary>
    /// The analyzed failure entity (saved to database)
    /// </summary>
    public FailureEntity? Failure { get; set; }

    /// <summary>
    /// Similar historical failures found
    /// </summary>
    public List<SimilarFailure>? SimilarFailures { get; set; }

    /// <summary>
    /// Pattern this failure belongs to (if any)
    /// </summary>
    public FailurePattern? AssignedPattern { get; set; }

    /// <summary>
    /// Similarity score to the assigned pattern
    /// </summary>
    public float? PatternSimilarity { get; set; }

    /// <summary>
    /// Related commits analyzed (if GitHub context provided)
    /// </summary>
    public List<CommitInfo>? RelatedCommits { get; set; }

    /// <summary>
    /// Commit correlation analysis
    /// </summary>
    public string? CommitCorrelation { get; set; }

    /// <summary>
    /// Detailed execution steps and their results
    /// </summary>
    public List<ExecutionStep> ExecutionSteps { get; set; } = new();

    /// <summary>
    /// Any errors encountered during orchestration
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Total tokens used across all LLM calls
    /// </summary>
    public int TotalInputTokens { get; set; }

    /// <summary>
    /// Total tokens generated across all LLM calls
    /// </summary>
    public int TotalOutputTokens { get; set; }

    /// <summary>
    /// Markdown-formatted comprehensive report
    /// </summary>
    public string? Report { get; set; }
}

/// <summary>
/// Status of an orchestration run
/// </summary>
public enum OrchestrationStatus
{
    Running,
    Completed,
    Failed,
    PartialSuccess
}

/// <summary>
/// Individual step in the orchestration pipeline
/// </summary>
public class ExecutionStep
{
    public string StepName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Similar failure information for orchestration results
/// </summary>
public class SimilarFailure
{
    public int FailureId { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public string RootCause { get; set; } = string.Empty;
    public float Similarity { get; set; }
    public DateTime AnalyzedAt { get; set; }
    public List<string>? Recommendations { get; set; }
}
