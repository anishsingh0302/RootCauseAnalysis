namespace AgenticPipeline.Core.Models;

/// <summary>
/// Intermediate model for deserializing Claude's JSON response for RCA analysis.
/// This is the raw format returned by Claude before mapping to RcaResult.
/// </summary>
/// <param name="RootCause">Brief summary of the root cause</param>
/// <param name="DetailedAnalysis">Detailed explanation of what went wrong</param>
/// <param name="AffectedComponents">List of affected files or components</param>
/// <param name="ErrorType">Type of error (e.g., CompilationError, TestFailure, DependencyIssue)</param>
/// <param name="ConfidenceScore">Confidence score (0.0 to 1.0)</param>
/// <param name="Recommendations">List of recommended fix steps</param>
public record RcaJsonResponse(
    string RootCause,
    string DetailedAnalysis,
    List<string>? AffectedComponents,
    string ErrorType,
    double ConfidenceScore,
    List<string>? Recommendations);
