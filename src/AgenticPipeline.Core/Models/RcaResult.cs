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
public record RcaResult(
    string RootCause,
    string DetailedAnalysis,
    List<string> AffectedComponents,
    string ErrorType,
    double ConfidenceScore,
    List<string> Recommendations,
    TokenUsage TokenUsage,
    DateTime AnalyzedAt);

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
