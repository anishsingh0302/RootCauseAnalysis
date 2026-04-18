namespace AgenticPipeline.Core.Models;

/// <summary>
/// Statistics about failure analyses
/// </summary>
public record FailureStatistics(
    int TotalFailures,
    double AverageConfidence,
    int TotalInputTokens,
    int TotalOutputTokens,
    Dictionary<string, int> FailuresByErrorType,
    DateTime? MostRecentAnalysis,
    DateTime? OldestAnalysis);
