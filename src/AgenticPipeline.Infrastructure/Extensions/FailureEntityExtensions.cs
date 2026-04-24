using System.Text.Json;
using AgenticPipeline.Core.Models;

namespace AgenticPipeline.Infrastructure.Extensions;

/// <summary>
/// Extension methods for mapping between RcaResult and FailureEntity
/// </summary>
public static class FailureEntityExtensions
{
    /// <summary>
    /// Converts RcaResult to FailureEntity for database persistence
    /// </summary>
    public static FailureEntity ToEntity(this RcaResult rcaResult)
    {
        return new FailureEntity
        {
            RootCause = rcaResult.RootCause,
            DetailedAnalysis = rcaResult.DetailedAnalysis,
            ErrorType = rcaResult.ErrorType,
            ConfidenceScore = rcaResult.ConfidenceScore,
            AnalyzedAt = rcaResult.AnalyzedAt,
            AffectedComponentsJson = JsonSerializer.Serialize(rcaResult.AffectedComponents),
            RecommendationsJson = JsonSerializer.Serialize(rcaResult.Recommendations),
            InputTokens = rcaResult.TokenUsage.InputTokens,
            OutputTokens = rcaResult.TokenUsage.OutputTokens,
            SuspectedCommitsJson = rcaResult.SuspectedCommits != null
                ? JsonSerializer.Serialize(rcaResult.SuspectedCommits)
                : null,
            Repository = rcaResult.CommitCorrelation?.Repository,
            CommitsAnalyzed = rcaResult.CommitCorrelation?.TotalCommitsAnalyzed
        };
    }

    /// <summary>
    /// Converts RcaResult with log content to FailureEntity
    /// </summary>
    public static FailureEntity ToEntity(this RcaResult rcaResult, string logContent)
    {
        var entity = rcaResult.ToEntity();
        entity.LogContent = logContent;
        return entity;
    }

    /// <summary>
    /// Converts FailureEntity back to RcaResult
    /// </summary>
    public static RcaResult ToRcaResult(this FailureEntity entity)
    {
        var affectedComponents = string.IsNullOrWhiteSpace(entity.AffectedComponentsJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(entity.AffectedComponentsJson) ?? new List<string>();

        var recommendations = string.IsNullOrWhiteSpace(entity.RecommendationsJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(entity.RecommendationsJson) ?? new List<string>();

        var suspectedCommits = !string.IsNullOrWhiteSpace(entity.SuspectedCommitsJson)
            ? JsonSerializer.Deserialize<List<SuspectedCommit>>(entity.SuspectedCommitsJson)
            : null;

        CommitCorrelationMetadata? commitCorrelation = null;
        if (entity.Repository != null && entity.CommitsAnalyzed.HasValue)
        {
            commitCorrelation = new CommitCorrelationMetadata(
                Repository: entity.Repository,
                TotalCommitsAnalyzed: entity.CommitsAnalyzed.Value,
                TimeRangeStart: entity.AnalyzedAt.AddHours(-24), // Default lookback
                TimeRangeEnd: entity.AnalyzedAt,
                LookbackHours: 24);
        }

        return new RcaResult(
            RootCause: entity.RootCause,
            DetailedAnalysis: entity.DetailedAnalysis,
            AffectedComponents: affectedComponents,
            ErrorType: entity.ErrorType,
            ConfidenceScore: entity.ConfidenceScore,
            Recommendations: recommendations,
            TokenUsage: new TokenUsage(entity.InputTokens, entity.OutputTokens),
            AnalyzedAt: entity.AnalyzedAt,
            SuspectedCommits: suspectedCommits,
            CommitCorrelation: commitCorrelation);
    }

    /// <summary>
    /// Converts SuspectedCommit to CommitEntity for database persistence
    /// </summary>
    public static CommitEntity ToEntity(this SuspectedCommit suspectedCommit)
    {
        return new CommitEntity
        {
            Sha = suspectedCommit.Sha,
            Author = suspectedCommit.Author,
            AuthorEmail = "", // Not available in SuspectedCommit
            CommitDate = suspectedCommit.CommitDate ?? DateTime.MinValue, // Use MinValue for unknown dates
            Message = suspectedCommit.Message,
            FilesChangedJson = JsonSerializer.Serialize(suspectedCommit.SuspectedFiles),
            ChangeType = "modified", // Default
            Additions = 0, // Not available in SuspectedCommit
            Deletions = 0, // Not available in SuspectedCommit
            Url = "", // Not available in SuspectedCommit
            ConfidenceScore = suspectedCommit.Confidence,
            SuspectedReason = suspectedCommit.Reason,
            Evidence = suspectedCommit.Evidence
        };
    }
}
