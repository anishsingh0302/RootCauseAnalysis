using AgenticPipeline.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace AgenticPipeline.Infrastructure.Plugins;

/// <summary>
/// Semantic Kernel plugin for analyzing GitHub commits in relation to pipeline failures.
/// </summary>
public class CommitAnalyzerPlugin
{
    private readonly ICommitAnalyzer _commitAnalyzer;
    private readonly IClaudeService _claudeService;
    private readonly ILogger<CommitAnalyzerPlugin> _logger;

    public CommitAnalyzerPlugin(
        ICommitAnalyzer commitAnalyzer,
        IClaudeService claudeService,
        ILogger<CommitAnalyzerPlugin> logger)
    {
        _commitAnalyzer = commitAnalyzer;
        _claudeService = claudeService;
        _logger = logger;
    }

    /// <summary>
    /// Analyzes commits from a GitHub repository to find potential causes of pipeline failures.
    /// </summary>
    /// <param name="owner">Repository owner (username or organization)</param>
    /// <param name="repo">Repository name</param>
    /// <param name="failureTimestamp">Timestamp when the failure occurred (ISO 8601 format)</param>
    /// <param name="lookbackHours">How many hours before the failure to look for commits (default 24)</param>
    /// <returns>JSON string containing commit analysis results</returns>
    [KernelFunction]
    [Description("Analyzes recent commits from a GitHub repository to identify potential causes of pipeline failures")]
    public async Task<string> AnalyzeCommitContext(
        [Description("Repository owner (e.g., 'microsoft')")] string owner,
        [Description("Repository name (e.g., 'dotnet')")] string repo,
        [Description("Failure timestamp in ISO 8601 format (e.g., '2026-04-01T10:30:00Z')")] string failureTimestamp,
        [Description("Hours before failure to search for commits (default 24)")] int lookbackHours = 24)
    {
        try
        {
            _logger.LogInformation(
                "Analyzing commits for {Owner}/{Repo} around {Timestamp} (lookback: {Hours}h)",
                owner, repo, failureTimestamp, lookbackHours);

            // Parse failure timestamp
            if (!DateTime.TryParse(failureTimestamp, out var failureTime))
            {
                _logger.LogError("Invalid timestamp format: {Timestamp}", failureTimestamp);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid timestamp format. Use ISO 8601 format (e.g., '2026-04-01T10:30:00Z')"
                });
            }

            // Calculate time range
            var since = failureTime.AddHours(-lookbackHours);
            var until = failureTime;

            _logger.LogInformation("Fetching commits from {Since} to {Until}", since, until);

            // Fetch commits in time range
            var commits = await _commitAnalyzer.GetCommitsByTimeRangeAsync(owner, repo, since, until);

            if (commits.Count == 0)
            {
                _logger.LogWarning("No commits found in the specified time range");
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    commitsFound = 0,
                    message = "No commits found in the specified time range",
                    timeRange = new { since, until }
                });
            }

            _logger.LogInformation("Found {Count} commits in time range", commits.Count);

            // Fetch detailed information for each commit
            var detailedCommits = new List<CommitInfo>();
            foreach (var commit in commits)
            {
                var detailed = await _commitAnalyzer.GetCommitDetailsAsync(owner, repo, commit.Sha);
                detailedCommits.Add(detailed);
            }

            // Build commit summary for analysis
            var commitSummary = BuildCommitSummary(detailedCommits);

            _logger.LogInformation("Built commit summary with {Count} commits", detailedCommits.Count);

            // Return structured data
            var result = new
            {
                success = true,
                repository = $"{owner}/{repo}",
                failureTimestamp = failureTime,
                timeRange = new { since, until },
                commitsFound = detailedCommits.Count,
                commits = detailedCommits.Select(c => new
                {
                    sha = c.Sha,
                    shortSha = c.Sha[..7],
                    message = c.Message,
                    author = c.Author,
                    authorEmail = c.AuthorEmail,
                    commitDate = c.CommitDate,
                    filesChanged = c.FilesChanged.Count,
                    additions = c.Additions,
                    deletions = c.Deletions,
                    url = c.Url,
                    files = c.FilesChanged.Select(f => new
                    {
                        fileName = f.FileName,
                        status = f.Status,
                        additions = f.Additions,
                        deletions = f.Deletions,
                        hasPatch = !string.IsNullOrEmpty(f.Patch)
                    }).ToList()
                }).ToList(),
                summary = commitSummary
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing commit context");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// Correlates commits with a pipeline failure log using Claude AI to identify likely causes.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="failureLog">The pipeline failure log content</param>
    /// <param name="failureTimestamp">When the failure occurred</param>
    /// <param name="lookbackHours">Hours to look back for commits</param>
    /// <returns>JSON string with AI analysis correlating commits to the failure</returns>
    [KernelFunction]
    [Description("Analyzes commits and correlates them with a pipeline failure using AI")]
    public async Task<string> CorrelateCommitsWithFailure(
        [Description("Repository owner")] string owner,
        [Description("Repository name")] string repo,
        [Description("Pipeline failure log content")] string failureLog,
        [Description("Failure timestamp (ISO 8601)")] string failureTimestamp,
        [Description("Hours to look back for commits")] int lookbackHours = 24)
    {
        try
        {
            _logger.LogInformation("Correlating commits with failure for {Owner}/{Repo}", owner, repo);

            // First, get the commit context
            var commitContextJson = await AnalyzeCommitContext(owner, repo, failureTimestamp, lookbackHours);
            var commitContext = JsonSerializer.Deserialize<JsonElement>(commitContextJson);

            if (!commitContext.GetProperty("success").GetBoolean())
            {
                return commitContextJson; // Return error from AnalyzeCommitContext
            }

            var commitsFound = commitContext.GetProperty("commitsFound").GetInt32();
            if (commitsFound == 0)
            {
                // Return consistent format with commitsAnalyzed = 0
                var emptyResult = new
                {
                    success = true,
                    repository = $"{owner}/{repo}",
                    commitsAnalyzed = 0,
                    message = "No commits found in the specified time range",
                    timestamp = DateTime.UtcNow
                };
                return JsonSerializer.Serialize(emptyResult, new JsonSerializerOptions { WriteIndented = true });
            }

            // Build correlation prompt
            var prompt = BuildCorrelationPrompt(failureLog, commitContext);

            _logger.LogInformation("Sending correlation request to Claude (prompt length: {Length})", prompt.Length);

            // Send to Claude for analysis
            var claudeResponse = await _claudeService.SendMessageAsync(prompt);

            _logger.LogInformation("Received correlation analysis from Claude");

            // Combine results
            var result = new
            {
                success = true,
                repository = $"{owner}/{repo}",
                commitsAnalyzed = commitsFound,
                commitContext = commitContext,
                aiAnalysis = claudeResponse,
                timestamp = DateTime.UtcNow
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error correlating commits with failure");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Builds a human-readable summary of commits.
    /// </summary>
    private string BuildCommitSummary(List<CommitInfo> commits)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Found {commits.Count} commit(s) in the time range:");
        sb.AppendLine();

        foreach (var commit in commits.OrderBy(c => c.CommitDate))
        {
            sb.AppendLine($"Commit: {commit.Sha[..7]} by {commit.Author}");
            sb.AppendLine($"Date: {commit.CommitDate:yyyy-MM-dd HH:mm:ss UTC}");
            sb.AppendLine($"Message: {commit.Message.Split('\n')[0]}");
            sb.AppendLine($"Changes: {commit.FilesChanged.Count} file(s), +{commit.Additions}/-{commit.Deletions} lines");

            if (commit.FilesChanged.Count > 0)
            {
                sb.AppendLine("Files changed:");
                foreach (var file in commit.FilesChanged.Take(10))
                {
                    sb.AppendLine($"  - {file.FileName} ({file.Status}) +{file.Additions}/-{file.Deletions}");
                }
                if (commit.FilesChanged.Count > 10)
                {
                    sb.AppendLine($"  ... and {commit.FilesChanged.Count - 10} more file(s)");
                }
            }

            sb.AppendLine($"URL: {commit.Url}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds a prompt for Claude to correlate commits with failure.
    /// </summary>
    private string BuildCorrelationPrompt(string failureLog, JsonElement commitContext)
    {
        var commits = commitContext.GetProperty("commits");

        var prompt = new StringBuilder();
        prompt.AppendLine("# Task: Correlate GitHub Commits with CI/CD Pipeline Failure");
        prompt.AppendLine();
        prompt.AppendLine("You are analyzing a CI/CD pipeline failure and recent commits to identify the likely cause.");
        prompt.AppendLine();
        prompt.AppendLine("## Pipeline Failure Log:");
        prompt.AppendLine("```");
        prompt.AppendLine(failureLog.Length > 5000 ? failureLog[..5000] + "\n... (truncated)" : failureLog);
        prompt.AppendLine("```");
        prompt.AppendLine();
        prompt.AppendLine("## Recent Commits:");
        prompt.AppendLine();

        foreach (var commit in commits.EnumerateArray())
        {
            var sha = commit.GetProperty("shortSha").GetString();
            var message = commit.GetProperty("message").GetString();
            var author = commit.GetProperty("author").GetString();
            var date = commit.GetProperty("commitDate").GetString();
            var filesChanged = commit.GetProperty("filesChanged").GetInt32();
            var additions = commit.GetProperty("additions").GetInt32();
            var deletions = commit.GetProperty("deletions").GetInt32();

            prompt.AppendLine($"### Commit {sha}");
            prompt.AppendLine($"- **Author:** {author}");
            prompt.AppendLine($"- **Date:** {date}");
            prompt.AppendLine($"- **Message:** {message?.Split('\n')[0]}");
            prompt.AppendLine($"- **Changes:** {filesChanged} file(s), +{additions}/-{deletions} lines");
            prompt.AppendLine();

            var files = commit.GetProperty("files");
            if (files.GetArrayLength() > 0)
            {
                prompt.AppendLine("**Files changed:**");
                foreach (var file in files.EnumerateArray())
                {
                    var fileName = file.GetProperty("fileName").GetString();
                    var status = file.GetProperty("status").GetString();
                    var fileAdditions = file.GetProperty("additions").GetInt32();
                    var fileDeletions = file.GetProperty("deletions").GetInt32();
                    prompt.AppendLine($"  - `{fileName}` ({status}) +{fileAdditions}/-{fileDeletions}");
                }
                prompt.AppendLine();
            }
        }

        prompt.AppendLine("## Analysis Instructions:");
        prompt.AppendLine();
        prompt.AppendLine("Please analyze the failure log and commits to determine:");
        prompt.AppendLine("1. Which commit(s) are most likely responsible for the failure?");
        prompt.AppendLine("2. What specific changes in those commits could have caused the failure?");
        prompt.AppendLine("3. What is the confidence level (0.0 - 1.0) for each suspected commit?");
        prompt.AppendLine("4. What evidence from the failure log supports your conclusion?");
        prompt.AppendLine("5. What are the recommended actions to fix the issue?");
        prompt.AppendLine();
        prompt.AppendLine("Respond in JSON format:");
        prompt.AppendLine("```json");
        prompt.AppendLine("{");
        prompt.AppendLine("  \"suspectedCommits\": [");
        prompt.AppendLine("    {");
        prompt.AppendLine("      \"sha\": \"commit SHA\",");
        prompt.AppendLine("      \"confidence\": 0.0-1.0,");
        prompt.AppendLine("      \"reason\": \"why this commit is suspected\",");
        prompt.AppendLine("      \"suspectedFiles\": [\"file1.cs\", \"file2.cs\"],");
        prompt.AppendLine("      \"evidence\": \"evidence from failure log\"");
        prompt.AppendLine("    }");
        prompt.AppendLine("  ],");
        prompt.AppendLine("  \"overallAnalysis\": \"summary of the analysis\",");
        prompt.AppendLine("  \"recommendations\": [\"action 1\", \"action 2\"]");
        prompt.AppendLine("}");
        prompt.AppendLine("```");

        return prompt.ToString();
    }
}
