using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticPipeline.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Service for fetching Azure DevOps build logs
/// Documentation: https://learn.microsoft.com/en-us/rest/api/azure/devops/build/builds/get-build-logs
/// </summary>
public class AzureDevOpsLogService
{
    private readonly HttpClient _httpClient;
    private readonly AzureDevOpsSettings _settings;
    private readonly ILogger<AzureDevOpsLogService> _logger;

    public AzureDevOpsLogService(
        HttpClient httpClient,
        IOptions<AzureDevOpsSettings> settings,
        ILogger<AzureDevOpsLogService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        // Configure Basic Authentication with PAT
        if (!string.IsNullOrEmpty(_settings.PersonalAccessToken))
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($":{_settings.PersonalAccessToken}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            _logger.LogInformation(
                "AzureDevOps PAT configured. Token length: {Length}, First 5 chars: {Prefix}",
                _settings.PersonalAccessToken.Length,
                _settings.PersonalAccessToken.Substring(0, Math.Min(5, _settings.PersonalAccessToken.Length)));
        }
        else
        {
            _logger.LogWarning("AzureDevOps PAT is NOT configured! Authentication will fail.");
        }

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Fetches all build logs from Azure DevOps with retry logic
    /// </summary>
    public async Task<string> FetchBuildLogsAsync(
        string organization,
        string project,
        int buildId,
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
                    "Fetching logs for Azure DevOps build {BuildId} in {Organization}/{Project} (attempt {Attempt}/{MaxRetries})",
                    buildId, organization, project, attempt, maxRetries);

                // Step 1: Get list of all log IDs for this build
                var logsListUrl = $"https://dev.azure.com/{organization}/{project}/_apis/build/builds/{buildId}/logs?api-version={_settings.ApiVersion}";

                var logsListResponse = await _httpClient.GetAsync(logsListUrl);
                logsListResponse.EnsureSuccessStatusCode();

                var logsListJson = await logsListResponse.Content.ReadAsStringAsync();
                var logsList = JsonSerializer.Deserialize<AzureDevOpsLogsListResponse>(logsListJson);

                if (logsList?.Value == null || logsList.Value.Count == 0)
                {
                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning(
                            "No logs available yet for build {BuildId}, retrying in {Delay}s",
                            buildId, delay.TotalSeconds);
                        await Task.Delay(delay);
                        delay = TimeSpan.FromSeconds(delay.TotalSeconds * 1.5); // Exponential backoff
                        continue;
                    }

                    throw new InvalidOperationException($"No logs found for build {buildId} after {maxRetries} attempts");
                }

                _logger.LogInformation(
                    "Found {Count} log entries for build {BuildId}",
                    logsList.Value.Count, buildId);

                // Step 2: Fetch each log and combine them
                var allLogs = new StringBuilder();
                allLogs.AppendLine($"=== Azure DevOps Build Logs ===");
                allLogs.AppendLine($"Build ID: {buildId}");
                allLogs.AppendLine($"Organization: {organization}");
                allLogs.AppendLine($"Project: {project}");
                allLogs.AppendLine($"Total Log Entries: {logsList.Value.Count}");
                allLogs.AppendLine();

                foreach (var logEntry in logsList.Value)
                {
                    try
                    {
                        // Fetch individual log content
                        var logUrl = $"https://dev.azure.com/{organization}/{project}/_apis/build/builds/{buildId}/logs/{logEntry.Id}?api-version={_settings.ApiVersion}";
                        var logResponse = await _httpClient.GetAsync(logUrl);

                        if (logResponse.IsSuccessStatusCode)
                        {
                            var logContent = await logResponse.Content.ReadAsStringAsync();

                            allLogs.AppendLine($"=== Log Entry {logEntry.Id}: {logEntry.Type} ===");
                            allLogs.AppendLine($"Created: {logEntry.CreatedOn:yyyy-MM-dd HH:mm:ss}");
                            allLogs.AppendLine($"Last Changed: {logEntry.LastChangedOn:yyyy-MM-dd HH:mm:ss}");
                            allLogs.AppendLine($"Line Count: {logEntry.LineCount}");
                            allLogs.AppendLine();
                            allLogs.AppendLine(logContent);
                            allLogs.AppendLine();
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Failed to fetch log entry {LogId} for build {BuildId}: {StatusCode}",
                                logEntry.Id, buildId, logResponse.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Error fetching log entry {LogId} for build {BuildId}",
                            logEntry.Id, buildId);
                    }
                }

                var combinedLogs = allLogs.ToString();

                _logger.LogInformation(
                    "Successfully fetched logs for build {BuildId}: {Size} characters, {Count} log entries (attempt {Attempt})",
                    buildId, combinedLogs.Length, logsList.Value.Count, attempt);

                return combinedLogs;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex,
                    "HTTP error fetching logs for build {BuildId}, attempt {Attempt}/{MaxRetries}. Retrying in {Delay}s",
                    buildId, attempt, maxRetries, delay.TotalSeconds);

                await Task.Delay(delay);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 1.5); // Exponential backoff
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex,
                    "Error fetching logs for build {BuildId}, attempt {Attempt}/{MaxRetries}. Retrying in {Delay}s",
                    buildId, attempt, maxRetries, delay.TotalSeconds);

                await Task.Delay(delay);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 1.5);
            }
        }

        throw new InvalidOperationException(
            $"Build {buildId} logs not available after {maxRetries} attempts");
    }

    /// <summary>
    /// Gets build information from Azure DevOps
    /// </summary>
    public async Task<AzureDevOpsBuildInfo?> GetBuildInfoAsync(
        string organization,
        string project,
        int buildId)
    {
        try
        {
            var buildUrl = $"https://dev.azure.com/{organization}/{project}/_apis/build/builds/{buildId}?api-version={_settings.ApiVersion}";
            var response = await _httpClient.GetAsync(buildUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AzureDevOpsBuildInfo>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching build info for build {BuildId}", buildId);
            return null;
        }
    }
}

/// <summary>
/// Response from Azure DevOps logs list API
/// </summary>
public class AzureDevOpsLogsListResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("value")]
    public List<AzureDevOpsLogEntry> Value { get; set; } = new();
}

/// <summary>
/// Individual log entry metadata
/// </summary>
public class AzureDevOpsLogEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("createdOn")]
    public DateTime CreatedOn { get; set; }

    [JsonPropertyName("lastChangedOn")]
    public DateTime LastChangedOn { get; set; }

    [JsonPropertyName("lineCount")]
    public int LineCount { get; set; }
}

/// <summary>
/// Build information from Azure DevOps
/// </summary>
public class AzureDevOpsBuildInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("buildNumber")]
    public string? BuildNumber { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("sourceBranch")]
    public string? SourceBranch { get; set; }

    [JsonPropertyName("sourceVersion")]
    public string? SourceVersion { get; set; }

    [JsonPropertyName("repository")]
    public AzureDevOpsRepositoryInfo? Repository { get; set; }
}

public class AzureDevOpsRepositoryInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }  // e.g., "GitHub", "TfsGit"

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
