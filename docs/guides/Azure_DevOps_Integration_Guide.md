# Azure DevOps Pipeline Integration Guide

## Overview

The Agentic Pipeline application can be extended to analyze Azure DevOps pipeline failures in addition to GitHub Actions. This guide explains how to add Azure DevOps support.

---

## Architecture Overview

### Current (GitHub Actions)

```
GitHub Actions Failure
    ↓
GitHub Webhook (workflow_run)
    ↓
WebhookController.ReceiveGitHubWebhook()
    ↓
GitHubActionsLogService.FetchWorkflowLogsAsync()
    ↓
PipelineOrchestrator.ExecuteAsync()
    ↓
RCA + Commit Analysis + Pattern Detection
    ↓
Auto-Fix PR (if enabled)
```

### Proposed (Multi-Platform)

```
GitHub Actions Failure          Azure DevOps Build/Release Failure
        ↓                                      ↓
GitHub Webhook                     Azure DevOps Service Hook
        ↓                                      ↓
    WebhookController.ReceiveGitHubWebhook()
                        ↓
    WebhookController.ReceiveAzureDevOpsWebhook()  ← NEW
                        ↓
        PlatformAbstraction Layer
                ↓               ↓
    GitHubActionsLogService   AzureDevOpsLogService  ← NEW
                        ↓
            PipelineOrchestrator
                        ↓
        RCA + Analysis + Auto-Fix
```

---

## What Needs to Be Built

### 1. Azure DevOps Webhook Models

**File: `src/AgenticPipeline.Api/Models/AzureDevOpsWebhookPayload.cs`**

```csharp
using System.Text.Json.Serialization;

namespace AgenticPipeline.Api.Models;

/// <summary>
/// Azure DevOps Service Hook payload
/// </summary>
public class AzureDevOpsWebhookPayload
{
    [JsonPropertyName("subscriptionId")]
    public string? SubscriptionId { get; set; }

    [JsonPropertyName("notificationId")]
    public int NotificationId { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("eventType")]
    public string? EventType { get; set; }  // build.complete, release.deploymentCompleted

    [JsonPropertyName("publisherId")]
    public string? PublisherId { get; set; }

    [JsonPropertyName("message")]
    public AzureDevOpsMessage? Message { get; set; }

    [JsonPropertyName("resource")]
    public AzureDevOpsBuildResource? Resource { get; set; }
}

public class AzureDevOpsMessage
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("html")]
    public string? Html { get; set; }
}

public class AzureDevOpsBuildResource
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("buildNumber")]
    public string? BuildNumber { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }  // completed

    [JsonPropertyName("result")]
    public string? Result { get; set; }  // succeeded, failed, partiallySucceeded, canceled

    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("finishTime")]
    public DateTime? FinishTime { get; set; }

    [JsonPropertyName("repository")]
    public AzureDevOpsRepository? Repository { get; set; }

    [JsonPropertyName("definition")]
    public AzureDevOpsBuildDefinition? Definition { get; set; }

    [JsonPropertyName("project")]
    public AzureDevOpsProject? Project { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("logs")]
    public AzureDevOpsLogs? Logs { get; set; }
}

public class AzureDevOpsRepository
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }  // TfsGit, GitHub, etc.
}

public class AzureDevOpsBuildDefinition
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class AzureDevOpsProject
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class AzureDevOpsLogs
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
```

### 2. Azure DevOps Settings

**File: `src/AgenticPipeline.Core/Models/AzureDevOpsSettings.cs`**

```csharp
namespace AgenticPipeline.Core.Models;

public record AzureDevOpsSettings
{
    public const string SectionName = "AzureDevOps";

    /// <summary>
    /// Azure DevOps organization name (e.g., "mycompany")
    /// </summary>
    public string? Organization { get; init; }

    /// <summary>
    /// Personal Access Token for Azure DevOps API
    /// </summary>
    public string? PersonalAccessToken { get; init; }

    /// <summary>
    /// Webhook secret for signature verification (optional)
    /// </summary>
    public string? WebhookSecret { get; init; }

    /// <summary>
    /// Default project name
    /// </summary>
    public string? DefaultProject { get; init; }

    /// <summary>
    /// API version (e.g., "7.1-preview.7")
    /// </summary>
    public string ApiVersion { get; init; } = "7.1-preview.7";
}
```

### 3. Azure DevOps Log Service

**File: `src/AgenticPipeline.Infrastructure/Services/AzureDevOpsLogService.cs`**

```csharp
using System.Net.Http.Headers;
using System.Text;
using AgenticPipeline.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Service for fetching Azure DevOps build logs
/// </summary>
public class AzureDevOpsLogService
{
    private readonly HttpClient _httpClient;
    private readonly AzureDevOpsSettings _settings;
    private readonly ILogger<AzureDevOpsLogService> _logger;

    public AzureDevOpsLogService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureDevOpsSettings> settings,
        ILogger<AzureDevOpsLogService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AzureDevOps");
        _settings = settings.Value;
        _logger = logger;

        // Configure authentication
        if (!string.IsNullOrEmpty(_settings.PersonalAccessToken))
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($":{_settings.PersonalAccessToken}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }
    }

    /// <summary>
    /// Fetches build logs from Azure DevOps
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
                    "Fetching logs for build {BuildId} in {Organization}/{Project} (attempt {Attempt}/{MaxRetries})",
                    buildId, organization, project, attempt, maxRetries);

                // Get list of logs
                var logsUrl = $"https://dev.azure.com/{organization}/{project}/_apis/build/builds/{buildId}/logs?api-version={_settings.ApiVersion}";
                var logsResponse = await _httpClient.GetAsync(logsUrl);
                logsResponse.EnsureSuccessStatusCode();

                var logsJson = await logsResponse.Content.ReadAsStringAsync();
                var logs = System.Text.Json.JsonSerializer.Deserialize<AzureDevOpsLogsResponse>(logsJson);

                if (logs?.Value == null || logs.Value.Length == 0)
                {
                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning("No logs available yet, retrying in {Delay}s", delay.TotalSeconds);
                        await Task.Delay(delay);
                        delay = TimeSpan.FromSeconds(delay.TotalSeconds * 1.5);
                        continue;
                    }

                    throw new InvalidOperationException($"No logs found for build {buildId}");
                }

                // Fetch each log and combine
                var allLogs = new StringBuilder();

                foreach (var log in logs.Value)
                {
                    var logUrl = $"https://dev.azure.com/{organization}/{project}/_apis/build/builds/{buildId}/logs/{log.Id}?api-version={_settings.ApiVersion}";
                    var logResponse = await _httpClient.GetAsync(logUrl);

                    if (logResponse.IsSuccessStatusCode)
                    {
                        var logContent = await logResponse.Content.ReadAsStringAsync();
                        allLogs.AppendLine($"=== Log {log.Id}: {log.Type} ===");
                        allLogs.AppendLine(logContent);
                        allLogs.AppendLine();
                    }
                }

                var combinedLogs = allLogs.ToString();

                _logger.LogInformation(
                    "Successfully fetched logs for build {BuildId}: {Size} characters (attempt {Attempt})",
                    buildId, combinedLogs.Length, attempt);

                return combinedLogs;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex,
                    "Failed to fetch logs for build {BuildId}, attempt {Attempt}/{MaxRetries}",
                    buildId, attempt, maxRetries);

                await Task.Delay(delay);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 1.5);
            }
        }

        throw new InvalidOperationException(
            $"Build {buildId} logs not found after {maxRetries} attempts");
    }
}

public class AzureDevOpsLogsResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("value")]
    public AzureDevOpsLogEntry[]? Value { get; set; }
}

public class AzureDevOpsLogEntry
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public int Id { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string? Type { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("url")]
    public string? Url { get; set; }
}
```

### 4. Azure DevOps Webhook Controller

**Add to: `src/AgenticPipeline.Api/Controllers/WebhookController.cs`**

```csharp
/// <summary>
/// Receives Azure DevOps service hook webhooks
/// </summary>
[HttpPost("azuredevops")]
[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> ReceiveAzureDevOpsWebhook(
    [FromBody] AzureDevOpsWebhookPayload payload)
{
    try
    {
        _logger.LogInformation(
            "Received Azure DevOps webhook: EventType={EventType}, BuildId={BuildId}",
            payload.EventType,
            payload.Resource?.Id);

        // Only process build.complete events that failed
        if (payload.EventType != "build.complete")
        {
            _logger.LogInformation("Ignoring non-build.complete event: {EventType}", payload.EventType);
            return Ok(new { Status = "Ignored", Reason = "Not a build.complete event" });
        }

        if (payload.Resource == null)
        {
            _logger.LogWarning("Webhook payload missing resource information");
            return BadRequest(new { Error = "Missing resource information" });
        }

        // Only process failed builds
        if (payload.Resource.Result != "failed" && payload.Resource.Result != "partiallySucceeded")
        {
            _logger.LogInformation(
                "Ignoring build with result: {Result}",
                payload.Resource.Result);
            return Ok(new { Status = "Ignored", Reason = $"Build result: {payload.Resource.Result}" });
        }

        // Process in background
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await ProcessAzureDevOpsBuildFailureAsync(payload, scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Azure DevOps webhook asynchronously");
            }
        });

        return Ok(new
        {
            Status = "Accepted",
            BuildId = payload.Resource.Id,
            Message = "Build failure analysis queued"
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing Azure DevOps webhook");
        return StatusCode(500, new { Error = "Internal server error" });
    }
}

private async Task ProcessAzureDevOpsBuildFailureAsync(
    AzureDevOpsWebhookPayload payload,
    IServiceProvider serviceProvider)
{
    try
    {
        var orchestrator = serviceProvider.GetService<IPipelineOrchestrator>();
        var logService = serviceProvider.GetService<AzureDevOpsLogService>();
        var teamsService = serviceProvider.GetService<TeamsNotificationService>();
        var autoFixService = serviceProvider.GetService<IAutoFixService>();

        if (orchestrator == null || logService == null || payload.Resource == null)
        {
            _logger.LogWarning("Required services not configured for Azure DevOps");
            return;
        }

        _logger.LogInformation(
            "Processing Azure DevOps build failure: BuildId={BuildId}, Project={Project}",
            payload.Resource.Id,
            payload.Resource.Project?.Name);

        // Fetch build logs
        var logs = await logService.FetchBuildLogsAsync(
            payload.Resource.Project?.Name ?? _azureDevOpsSettings.Organization ?? "",
            payload.Resource.Project?.Name ?? _azureDevOpsSettings.DefaultProject ?? "",
            payload.Resource.Id);

        if (string.IsNullOrWhiteSpace(logs))
        {
            _logger.LogWarning("No logs found for build {BuildId}", payload.Resource.Id);
            return;
        }

        // Create orchestration request
        var request = new OrchestrationRequest
        {
            LogContent = logs,
            GitHubOwner = payload.Resource.Project?.Name ?? "",
            GitHubRepo = payload.Resource.Repository?.Name ?? "",
            FailureTimestamp = payload.Resource.FinishTime ?? DateTime.UtcNow,
            CommitLookbackDays = 7,
            SimilarFailuresCount = 5,
            MinSimilarity = 0.7f,
            EnablePatternDetection = true,
            EnableCommitAnalysis = true,
            Source = "Azure DevOps Service Hook",
            Metadata = new Dictionary<string, string>
            {
                ["BuildId"] = payload.Resource.Id.ToString(),
                ["BuildNumber"] = payload.Resource.BuildNumber ?? "",
                ["BuildDefinition"] = payload.Resource.Definition?.Name ?? "",
                ["Project"] = payload.Resource.Project?.Name ?? "",
                ["BuildUrl"] = payload.Resource.Url ?? ""
            }
        };

        // Execute orchestration
        var result = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        _logger.LogInformation(
            "Azure DevOps build analysis completed: OrchestrationId={OrchestrationId}, Status={Status}",
            result.OrchestrationId,
            result.Status);

        // Send notifications (same as GitHub Actions)
        await SendNotificationsAsync(
            ConvertToGitHubPayload(payload), // Helper to convert for notification
            result,
            teamsService,
            null, // No GitHub comment service for Azure DevOps
            autoFixService);
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error processing Azure DevOps build failure for build {BuildId}",
            payload.Resource?.Id);
    }
}
```

### 5. Configuration

**Add to `appsettings.json`:**

```json
{
  "AzureDevOps": {
    "Organization": "your-org-name",
    "PersonalAccessToken": "",  // Use environment variable
    "DefaultProject": "YourProject",
    "WebhookSecret": "",
    "ApiVersion": "7.1-preview.7"
  }
}
```

**Add to `Program.cs`:**

```csharp
// Configure Azure DevOps settings
builder.Services.Configure<AzureDevOpsSettings>(
    builder.Configuration.GetSection(AzureDevOpsSettings.SectionName));

// Register Azure DevOps log service
builder.Services.AddHttpClient<AzureDevOpsLogService>();
builder.Services.AddSingleton<AzureDevOpsLogService>();
```

---

## Setting Up Azure DevOps Service Hooks

### 1. Create Personal Access Token (PAT)

1. Go to Azure DevOps → User Settings → Personal Access Tokens
2. Create new token with these scopes:
   - **Build**: Read
   - **Code**: Read (for commit analysis)
   - **Project and Team**: Read

### 2. Configure Service Hook

1. Go to Project Settings → Service Hooks
2. Click "+" to create new service hook
3. Select "Web Hooks"
4. Configure:

**Trigger:**
- **Build completed**
- Status: Failed, Partially Succeeded

**Action:**
- **URL**: `https://your-api.ngrok-free.dev/api/webhook/azuredevops`
- **HTTP Headers**: (optional, for authentication)
- **Messages to send**: All
- **Detailed messages to send**: All

5. Test the hook
6. Finish

### 3. Set Environment Variable

```powershell
$env:AZUREDEVOPS__PERSONALACCESSTOKEN = "your-pat-here"
```

---

## Testing

### 1. Create Test Pipeline

**azure-pipelines.yml:**
```yaml
trigger:
  - main

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      version: '9.0.x'

  - script: |
      echo "Testing Azure DevOps integration"
      exit 1  # Force failure
    displayName: 'Intentional Failure'
```

### 2. Trigger Build

Queue a new build and watch for:
1. Build fails in Azure DevOps
2. Service hook triggers
3. Webhook received in API
4. Logs fetched
5. RCA analysis completes
6. Teams notification sent
7. Auto-fix PR created (if applicable)

---

## Comparison: GitHub Actions vs Azure DevOps Integration

| Feature | GitHub Actions | Azure DevOps | Status |
|---------|---------------|--------------|--------|
| Webhook endpoint | ✅ Implemented | 🔧 To implement | |
| Log fetching | ✅ Implemented | 🔧 To implement | |
| Commit analysis | ✅ Works | ✅ Compatible | GitHub API |
| Teams notification | ✅ Works | ✅ Compatible | Platform-agnostic |
| Auto-fix PR | ✅ GitHub PRs | ⚠️ GitHub only | Azure Repos not supported yet |

---

## Limitations for Azure DevOps

### 1. Auto-Fix PRs
- Currently only supports GitHub repositories
- Azure Repos (TFVC/Git) would need separate implementation
- Workaround: Use Azure Repos with GitHub mirror

### 2. Commit Analysis
- Works if using GitHub as source
- Limited if using Azure Repos
- Would need Azure Repos API integration

### 3. Pattern Detection
- ✅ Works - platform-agnostic
- Uses embeddings and clustering

---

## Implementation Effort

### Phase 1: Basic Integration (2-3 hours)
- ✅ Webhook models
- ✅ Webhook endpoint
- ✅ Log fetching service
- ✅ Configuration

### Phase 2: Full Features (3-5 hours)
- ✅ Service hook setup
- ✅ Testing
- ✅ Documentation
- ⚠️ Azure Repos integration (if needed)

### Phase 3: Advanced (5+ hours)
- ⚠️ Azure Repos commit analysis
- ⚠️ Azure Repos PR creation (for auto-fix)
- ⚠️ Azure Boards integration (link work items)

---

## Multi-Platform Architecture

### Abstraction Layer (Future)

If supporting many platforms, consider:

```csharp
public interface IPlatformAdapter
{
    Task<string> FetchLogsAsync(string identifier);
    Task<IEnumerable<Commit>> GetRecentCommitsAsync(string repo, DateTime since);
    Task<int?> CreateFixPullRequestAsync(AutoFixResult fix, string repo);
}

public class GitHubAdapter : IPlatformAdapter { }
public class AzureDevOpsAdapter : IPlatformAdapter { }
public class GitLabAdapter : IPlatformAdapter { }  // Future
public class JenkinsAdapter : IPlatformAdapter { }  // Future
```

This would allow:
```csharp
var adapter = GetAdapter(request.Source); // Factory pattern
var logs = await adapter.FetchLogsAsync(buildId);
```

---

## Summary

**Yes, you can absolutely verify Azure DevOps CI/CD pipelines!**

**What's needed:**
1. ✅ Webhook endpoint (2 hours)
2. ✅ Log fetching service (2 hours)
3. ✅ Configuration (30 min)
4. ✅ Service hook setup (30 min)

**What already works:**
- ✅ RCA analysis (platform-agnostic)
- ✅ Pattern detection (platform-agnostic)
- ✅ Similarity search (platform-agnostic)
- ✅ Teams notifications (platform-agnostic)

**What needs adaptation:**
- ⚠️ Commit analysis (if using Azure Repos)
- ⚠️ Auto-fix PRs (if using Azure Repos)

**Recommendation:**
Start with **Phase 1** (basic integration) which gives you:
- Azure DevOps build failure detection
- RCA analysis
- Pattern detection
- Teams notifications

Then add Azure Repos support if needed (Phase 2/3).

Would you like me to implement the Azure DevOps integration now?
