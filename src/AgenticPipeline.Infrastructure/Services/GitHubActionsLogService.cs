using Microsoft.Extensions.Logging;
using Octokit;
using System.IO.Compression;
using System.Text;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Service for fetching GitHub Actions workflow run logs
/// </summary>
public class GitHubActionsLogService
{
    private readonly GitHubClient _client;
    private readonly ILogger<GitHubActionsLogService> _logger;

    public GitHubActionsLogService(GitHubClient client, ILogger<GitHubActionsLogService> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Fetches logs for a GitHub Actions workflow run with retry logic
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="runId">Workflow run ID</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 5)</param>
    /// <param name="initialDelaySeconds">Initial delay between retries in seconds (default: 5)</param>
    /// <returns>Combined log content from all jobs</returns>
    public async Task<string> FetchWorkflowLogsAsync(
        string owner,
        string repo,
        long runId,
        int maxRetries = 5,
        int initialDelaySeconds = 5)
    {
        var attempt = 0;
        var delay = TimeSpan.FromSeconds(initialDelaySeconds);

        while (attempt < maxRetries)
        {
            attempt++;

            try
            {
                _logger.LogInformation(
                    "Fetching logs for workflow run {RunId} in {Owner}/{Repo} (attempt {Attempt}/{MaxRetries})",
                    runId,
                    owner,
                    repo,
                    attempt,
                    maxRetries);

                // GitHub API returns logs as a ZIP archive (byte array)
                var logsBytes = await _client.Actions.Workflows.Runs.GetLogs(owner, repo, runId);
                using var memoryStream = new MemoryStream(logsBytes);
                memoryStream.Position = 0;

                var logContent = new StringBuilder();

                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                        {
                            using var entryStream = entry.Open();
                            using var reader = new StreamReader(entryStream);
                            var content = await reader.ReadToEndAsync();

                            logContent.AppendLine($"=== Log: {entry.FullName} ===");
                            logContent.AppendLine(content);
                            logContent.AppendLine();
                        }
                    }
                }

                var logs = logContent.ToString();

                _logger.LogInformation(
                    "Successfully fetched logs for run {RunId}: {Size} characters (attempt {Attempt})",
                    runId,
                    logs.Length,
                    attempt);

                return logs;
            }
            catch (NotFoundException)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(
                        "Workflow run {RunId} logs not yet available in {Owner}/{Repo} (attempt {Attempt}/{MaxRetries}). " +
                        "Logs may not be ready yet. Retrying in {Delay} seconds...",
                        runId,
                        owner,
                        repo,
                        attempt,
                        maxRetries,
                        delay.TotalSeconds);

                    await Task.Delay(delay);
                    delay = TimeSpan.FromSeconds(delay.TotalSeconds * 1.5); // Exponential backoff
                }
                else
                {
                    _logger.LogError(
                        "Workflow run {RunId} logs not found in {Owner}/{Repo} after {Attempts} attempts. " +
                        "The workflow may not generate logs, logs may have expired, or there may be a permissions issue.",
                        runId,
                        owner,
                        repo,
                        maxRetries);

                    throw new InvalidOperationException(
                        $"Workflow run {runId} logs not found after {maxRetries} attempts. " +
                        "Logs may not be available yet, may have expired, or the workflow may not generate logs.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to fetch logs for workflow run {RunId} in {Owner}/{Repo} on attempt {Attempt}/{MaxRetries}",
                    runId,
                    owner,
                    repo,
                    attempt,
                    maxRetries);

                if (attempt >= maxRetries)
                {
                    throw;
                }

                await Task.Delay(delay);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 1.5);
            }
        }

        throw new InvalidOperationException($"Failed to fetch logs for workflow run {runId} after {maxRetries} attempts");
    }

    /// <summary>
    /// Gets workflow run details
    /// </summary>
    public async Task<WorkflowRun> GetWorkflowRunAsync(string owner, string repo, long runId)
    {
        try
        {
            return await _client.Actions.Workflows.Runs.Get(owner, repo, runId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get workflow run {RunId} in {Owner}/{Repo}",
                runId,
                owner,
                repo);
            throw;
        }
    }
}
