using AgenticPipeline.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Service for posting analysis results as GitHub PR/issue comments
/// </summary>
public class GitHubCommentService
{
    private readonly GitHubClient _client;
    private readonly GitHubSettings _settings;
    private readonly ILogger<GitHubCommentService> _logger;

    public GitHubCommentService(
        GitHubClient client,
        IOptions<GitHubSettings> settings,
        ILogger<GitHubCommentService> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Posts orchestration results as a comment on a pull request
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="pullRequestNumber">PR number</param>
    /// <param name="result">Orchestration result to post</param>
    public async Task<IssueComment> PostToPullRequestAsync(
        string owner,
        string repo,
        int pullRequestNumber,
        OrchestrationResult result)
    {
        try
        {
            _logger.LogInformation(
                "Posting analysis results to PR #{PRNumber} in {Owner}/{Repo}",
                pullRequestNumber, owner, repo);

            var comment = FormatPullRequestComment(result);

            var issueComment = await _client.Issue.Comment.Create(
                owner,
                repo,
                pullRequestNumber,
                comment);

            _logger.LogInformation(
                "Successfully posted comment {CommentId} to PR #{PRNumber}",
                issueComment.Id, pullRequestNumber);

            return issueComment;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to post comment to PR #{PRNumber} in {Owner}/{Repo}",
                pullRequestNumber, owner, repo);
            throw;
        }
    }

    /// <summary>
    /// Posts orchestration results as a comment on an issue
    /// </summary>
    public async Task<IssueComment> PostToIssueAsync(
        string owner,
        string repo,
        int issueNumber,
        OrchestrationResult result)
    {
        try
        {
            _logger.LogInformation(
                "Posting analysis results to Issue #{IssueNumber} in {Owner}/{Repo}",
                issueNumber, owner, repo);

            var comment = FormatIssueComment(result);

            var issueComment = await _client.Issue.Comment.Create(
                owner,
                repo,
                issueNumber,
                comment);

            _logger.LogInformation(
                "Successfully posted comment {CommentId} to Issue #{IssueNumber}",
                issueComment.Id, issueNumber);

            return issueComment;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to post comment to Issue #{IssueNumber} in {Owner}/{Repo}",
                issueNumber, owner, repo);
            throw;
        }
    }

    /// <summary>
    /// Creates an issue for a new failure pattern
    /// </summary>
    public async Task<Issue> CreateIssueForPatternAsync(
        string owner,
        string repo,
        FailurePattern pattern,
        OrchestrationResult result)
    {
        try
        {
            _logger.LogInformation(
                "Creating issue for pattern '{PatternName}' in {Owner}/{Repo}",
                pattern.Name, owner, repo);

            var title = $"[Automated] Pattern Detected: {pattern.Name}";
            var body = FormatPatternIssue(pattern, result);

            var labels = new[] { "automated", "failure-pattern", pattern.PrimaryErrorType.ToLower() };

            var newIssue = new NewIssue(title)
            {
                Body = body
            };

            foreach (var label in labels)
            {
                newIssue.Labels.Add(label);
            }

            var issue = await _client.Issue.Create(owner, repo, newIssue);

            _logger.LogInformation(
                "Successfully created issue #{IssueNumber} for pattern",
                issue.Number);

            return issue;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create issue for pattern '{PatternName}' in {Owner}/{Repo}",
                pattern.Name, owner, repo);
            throw;
        }
    }

    /// <summary>
    /// Formats orchestration result as a PR comment
    /// </summary>
    private string FormatPullRequestComment(OrchestrationResult result)
    {
        if (result.Failure == null)
        {
            return "⚠️ **Failure Analysis Incomplete**\n\nThe analysis did not complete successfully.";
        }

        var comment = new System.Text.StringBuilder();

        // Header
        comment.AppendLine("## 🔍 Automated Failure Analysis");
        comment.AppendLine();
        comment.AppendLine($"> **Orchestration ID:** `{result.OrchestrationId}`");
        comment.AppendLine($"> **Analyzed:** {result.StartedAt:yyyy-MM-dd HH:mm:ss UTC}");
        comment.AppendLine($"> **Duration:** {result.Duration?.TotalSeconds:F1}s");
        comment.AppendLine();

        // Status badge
        var statusEmoji = result.Status switch
        {
            OrchestrationStatus.Completed => "✅",
            OrchestrationStatus.PartialSuccess => "⚠️",
            OrchestrationStatus.Failed => "❌",
            _ => "🔄"
        };
        comment.AppendLine($"**Status:** {statusEmoji} {result.Status}");
        comment.AppendLine();

        // Root Cause
        comment.AppendLine("### 🎯 Root Cause");
        comment.AppendLine();
        comment.AppendLine($"**Error Type:** `{result.Failure.ErrorType}`");
        comment.AppendLine($"**Confidence:** {result.Failure.ConfidenceScore:P0}");
        comment.AppendLine();
        comment.AppendLine($"> {result.Failure.RootCause}");
        comment.AppendLine();

        // Recommendations
        var recommendations = System.Text.Json.JsonSerializer.Deserialize<List<string>>(
            result.Failure.RecommendationsJson);
        if (recommendations != null && recommendations.Count > 0)
        {
            comment.AppendLine("### 💡 Recommended Actions");
            comment.AppendLine();
            foreach (var rec in recommendations.Take(5))
            {
                comment.AppendLine($"- {rec}");
            }
            comment.AppendLine();
        }

        // Similar Failures
        if (result.SimilarFailures != null && result.SimilarFailures.Count > 0)
        {
            comment.AppendLine("### 🔗 Similar Past Failures");
            comment.AppendLine();
            comment.AppendLine($"Found {result.SimilarFailures.Count} similar failure(s):");
            comment.AppendLine();

            foreach (var similar in result.SimilarFailures.Take(3))
            {
                comment.AppendLine($"- **Failure #{similar.FailureId}** ({similar.Similarity:P0} similar) - {similar.ErrorType}");
                comment.AppendLine($"  - *{similar.RootCause}*");
            }
            comment.AppendLine();
        }

        // Pattern Detection
        if (result.AssignedPattern != null)
        {
            comment.AppendLine("### 📊 Pattern Recognition");
            comment.AppendLine();
            comment.AppendLine($"This failure matches the pattern: **{result.AssignedPattern.Name}**");
            comment.AppendLine();
            comment.AppendLine($"- **Frequency:** {result.AssignedPattern.Frequency:F1} failures/day");
            comment.AppendLine($"- **Total Occurrences:** {result.AssignedPattern.FailureCount}");
            comment.AppendLine($"- **Pattern Confidence:** {result.PatternSimilarity:P0}");
            comment.AppendLine();
        }

        // Commit Correlation
        if (result.RelatedCommits != null && result.RelatedCommits.Count > 0)
        {
            comment.AppendLine("### 📝 Related Commits");
            comment.AppendLine();
            comment.AppendLine($"Found {result.RelatedCommits.Count} recent commit(s):");
            comment.AppendLine();

            foreach (var commit in result.RelatedCommits.Take(3))
            {
                comment.AppendLine($"- [`{commit.Sha[..7]}`]({commit.Url}) {commit.Message.Split('\n')[0]} - *{commit.Author}*");
            }
            comment.AppendLine();
        }

        // Footer
        comment.AppendLine("---");
        comment.AppendLine();
        comment.AppendLine($"<details>");
        comment.AppendLine($"<summary>📊 Analysis Details</summary>");
        comment.AppendLine();
        comment.AppendLine("**Execution Steps:**");
        comment.AppendLine();
        foreach (var step in result.ExecutionSteps)
        {
            var stepEmoji = step.Success ? "✅" : "❌";
            comment.AppendLine($"- {stepEmoji} {step.StepName}: {step.Duration?.TotalSeconds:F2}s");
        }
        comment.AppendLine();
        comment.AppendLine($"**Token Usage:** {result.TotalInputTokens + result.TotalOutputTokens:N0} tokens");
        comment.AppendLine();
        comment.AppendLine("</details>");
        comment.AppendLine();
        comment.AppendLine("*🤖 Generated by [Agentic Pipeline](https://github.com/anishsingh0302/Agentic_Pipeline) v4.0*");

        return comment.ToString();
    }

    /// <summary>
    /// Formats orchestration result as an issue comment
    /// </summary>
    private string FormatIssueComment(OrchestrationResult result)
    {
        // Similar format to PR comment but more detailed
        return FormatPullRequestComment(result);
    }

    /// <summary>
    /// Formats a pattern detection result as an issue
    /// </summary>
    private string FormatPatternIssue(FailurePattern pattern, OrchestrationResult result)
    {
        var issue = new System.Text.StringBuilder();

        issue.AppendLine("## 📊 Failure Pattern Detected");
        issue.AppendLine();
        issue.AppendLine($"A recurring failure pattern has been detected: **{pattern.Name}**");
        issue.AppendLine();

        issue.AppendLine("### Pattern Details");
        issue.AppendLine();
        issue.AppendLine($"- **Primary Error Type:** `{pattern.PrimaryErrorType}`");
        issue.AppendLine($"- **Frequency:** {pattern.Frequency:F2} failures/day");
        issue.AppendLine($"- **Total Occurrences:** {pattern.FailureCount}");
        issue.AppendLine($"- **First Seen:** {pattern.FirstOccurrence:yyyy-MM-dd}");
        issue.AppendLine($"- **Last Seen:** {pattern.LastOccurrence:yyyy-MM-dd}");
        issue.AppendLine($"- **Average Confidence:** {pattern.AverageConfidence:P0}");
        issue.AppendLine();

        if (pattern.CommonRootCauses.Count > 0)
        {
            issue.AppendLine("### Common Root Causes");
            issue.AppendLine();
            foreach (var cause in pattern.CommonRootCauses.Take(5))
            {
                issue.AppendLine($"- {cause}");
            }
            issue.AppendLine();
        }

        if (pattern.CommonComponents.Count > 0)
        {
            issue.AppendLine("### Affected Components");
            issue.AppendLine();
            foreach (var component in pattern.CommonComponents.Take(10))
            {
                issue.AppendLine($"- `{component}`");
            }
            issue.AppendLine();
        }

        issue.AppendLine("### Recent Example");
        issue.AppendLine();
        if (result.Failure != null)
        {
            issue.AppendLine($"**Error Type:** `{result.Failure.ErrorType}`");
            issue.AppendLine($"**Root Cause:** {result.Failure.RootCause}");
            issue.AppendLine($"**Confidence:** {result.Failure.ConfidenceScore:P0}");
        }
        issue.AppendLine();

        issue.AppendLine("### Recommended Actions");
        issue.AppendLine();
        issue.AppendLine("1. Review the common root causes listed above");
        issue.AppendLine("2. Check if recent code changes affect the listed components");
        issue.AppendLine("3. Consider implementing automated monitoring for this pattern");
        issue.AppendLine("4. Update error handling in affected components");
        issue.AppendLine();

        issue.AppendLine("---");
        issue.AppendLine();
        issue.AppendLine($"*🤖 Automatically generated by Agentic Pipeline on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}*");

        return issue.ToString();
    }
}
