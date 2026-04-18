namespace AgenticPipeline.Core.Models;

/// <summary>
/// Root Cause Analysis result
/// </summary>
/// <param name="RootCause">Brief summary of the root cause</param>
/// <param name="DetailedAnalysis">Detailed analysis of the failure</param>
/// <param name="AffectedComponents">Components or files affected by the failure</param>
/// <param name="ErrorType">Classification of the error type (e.g., CompilationError, TestFailure, DependencyIssue)</param>
/// <param name="ConfidenceScore">Confidence score of the analysis (0.0 to 1.0)</param>
/// <param name="Recommendations">Recommended fix suggestions</param>
/// <param name="TokenUsage">Token usage for this analysis</param>
/// <param name="AnalyzedAt">Timestamp when the analysis was performed</param>
/// <param name="SuspectedCommits">List of commits suspected to be related to the failure (null if no commit analysis performed)</param>
/// <param name="CommitCorrelation">Metadata about commit correlation analysis (null if not performed)</param>
public record RcaResult(
    string RootCause,
    string DetailedAnalysis,
    List<string> AffectedComponents,
    string ErrorType,
    double ConfidenceScore,
    List<string> Recommendations,
    TokenUsage TokenUsage,
    DateTime AnalyzedAt,
    List<SuspectedCommit>? SuspectedCommits = null,
    CommitCorrelationMetadata? CommitCorrelation = null);

/// <summary>
/// Represents a commit suspected to be related to a failure
/// </summary>
/// <param name="Sha">Commit SHA</param>
/// <param name="ShortSha">Short version of commit SHA (first 7 chars)</param>
/// <param name="Message">Commit message</param>
/// <param name="Author">Commit author</param>
/// <param name="CommitDate">Date of the commit</param>
/// <param name="Confidence">Confidence score that this commit caused the failure (0.0 to 1.0)</param>
/// <param name="Reason">Explanation of why this commit is suspected</param>
/// <param name="SuspectedFiles">Files in this commit that are likely related to the failure</param>
/// <param name="Evidence">Evidence from the failure log that links to this commit</param>
public record SuspectedCommit(
    string Sha,
    string ShortSha,
    string Message,
    string Author,
    DateTime CommitDate,
    double Confidence,
    string Reason,
    List<string> SuspectedFiles,
    string Evidence);

/// <summary>
/// Metadata about commit correlation analysis
/// </summary>
/// <param name="Repository">Repository analyzed (owner/repo)</param>
/// <param name="TotalCommitsAnalyzed">Total number of commits analyzed</param>
/// <param name="TimeRangeStart">Start of time range for commit search</param>
/// <param name="TimeRangeEnd">End of time range for commit search</param>
/// <param name="LookbackHours">Hours looked back from failure time</param>
public record CommitCorrelationMetadata(
    string Repository,
    int TotalCommitsAnalyzed,
    DateTime TimeRangeStart,
    DateTime TimeRangeEnd,
    int LookbackHours);

/// <summary>
/// Token usage tracking
/// </summary>
/// <param name="InputTokens">Number of input tokens used</param>
/// <param name="OutputTokens">Number of output tokens generated</param>
public record TokenUsage(int InputTokens, int OutputTokens)
{
    /// <summary>
    /// Total tokens (input + output)
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;
}
