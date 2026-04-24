using AgenticPipeline.Api.Models;
using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using GitHubSettings = AgenticPipeline.Infrastructure.Services.GitHubSettings;

namespace AgenticPipeline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WebhookVerificationService _verificationService;
    private readonly GitHubSettings _gitHubSettings;
    private readonly NotificationSettings _notificationSettings;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IServiceScopeFactory scopeFactory,
        WebhookVerificationService verificationService,
        IOptions<GitHubSettings> gitHubSettings,
        IOptions<NotificationSettings> notificationSettings,
        ILogger<WebhookController> logger)
    {
        _scopeFactory = scopeFactory;
        _verificationService = verificationService;
        _gitHubSettings = gitHubSettings.Value;
        _notificationSettings = notificationSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Receives GitHub Actions workflow run webhook events
    /// </summary>
    /// <param name="payload">GitHub webhook payload</param>
    /// <returns>Acknowledgment response</returns>
    [HttpPost("github")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> ReceiveGitHubWebhook([FromBody] GitHubWebhookPayload payload)
    {
        try
        {
            _logger.LogInformation(
                "Received GitHub webhook: Action={Action}, WorkflowRunId={RunId}",
                payload.Action, payload.WorkflowRun?.Id);

            // Verify webhook signature if secret is configured
            if (!string.IsNullOrEmpty(_gitHubSettings.WebhookSecret))
            {
                var signatureHeader = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
                if (string.IsNullOrEmpty(signatureHeader))
                {
                    _logger.LogWarning("Webhook signature verification failed: no signature header");
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Unauthorized",
                        Detail = "Missing webhook signature",
                        Status = StatusCodes.Status401Unauthorized
                    });
                }

                // Read raw body for signature verification
                Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                var rawBody = await reader.ReadToEndAsync();
                Request.Body.Position = 0; // Reset for potential future reads

                if (!_verificationService.VerifyGitHubSignature(rawBody, signatureHeader, _gitHubSettings.WebhookSecret))
                {
                    _logger.LogWarning("Webhook signature verification failed");
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Unauthorized",
                        Detail = "Invalid webhook signature",
                        Status = StatusCodes.Status401Unauthorized
                    });
                }
            }

            // Only process completed workflow runs that failed
            // GitHub may send action="completed" OR action="requested" with status="completed"
            bool isCompleted = payload.Action == "completed" ||
                             (payload.Action == "requested" && payload.WorkflowRun?.Status == "completed");
            bool isFailed = payload.WorkflowRun?.Conclusion == "failure";

            if (!isCompleted || !isFailed)
            {
                _logger.LogInformation(
                    "Ignoring webhook: Action={Action}, Status={Status}, Conclusion={Conclusion}",
                    payload.Action, payload.WorkflowRun?.Status, payload.WorkflowRun?.Conclusion);

                return Ok(new
                {
                    Message = "Webhook received but not processed (not a completed failure)",
                    Action = payload.Action,
                    Status = payload.WorkflowRun?.Status,
                    Conclusion = payload.WorkflowRun?.Conclusion
                });
            }

            // Process the webhook asynchronously (fire and forget) with a new DI scope
            _ = Task.Run(async () =>
            {
                try
                {
                    // Create a new scope for the background task to avoid DbContext disposal issues
                    using var scope = _scopeFactory.CreateScope();
                    await ProcessWorkflowFailureAsync(payload, scope.ServiceProvider);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing webhook asynchronously");
                }
            });

            return Ok(new
            {
                Message = "Webhook received and queued for processing",
                WorkflowRunId = payload.WorkflowRun.Id,
                Repository = payload.Repository?.FullName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GitHub webhook");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Webhook Processing Error",
                Detail = $"An error occurred while processing the webhook: {ex.Message}",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    private async Task ProcessWorkflowFailureAsync(GitHubWebhookPayload payload, IServiceProvider serviceProvider)
    {
        try
        {
            if (payload.WorkflowRun == null || payload.Repository == null)
                return;

            // Resolve services from the scoped service provider
            var orchestrator = serviceProvider.GetService<IPipelineOrchestrator>();
            var logService = serviceProvider.GetService<GitHubActionsLogService>();
            var teamsService = serviceProvider.GetService<TeamsNotificationService>();
            var githubCommentService = serviceProvider.GetService<GitHubCommentService>();
            var autoFixService = serviceProvider.GetService<IAutoFixService>();

            if (orchestrator == null || logService == null)
            {
                _logger.LogWarning("Orchestrator or log service not configured");
                return;
            }

            _logger.LogInformation(
                "Processing workflow failure: RunId={RunId}, Repo={Repo}",
                payload.WorkflowRun.Id, payload.Repository.FullName);

            // Fetch workflow logs
            var logs = await logService.FetchWorkflowLogsAsync(
                payload.Repository.Owner!.Login,
                payload.Repository.Name,
                payload.WorkflowRun.Id);

            if (string.IsNullOrWhiteSpace(logs))
            {
                _logger.LogWarning(
                    "No logs found for workflow run {RunId}",
                    payload.WorkflowRun.Id);
                return;
            }

            // Create orchestration request
            var request = new OrchestrationRequest
            {
                LogContent = logs,
                GitHubOwner = payload.Repository.Owner.Login,
                GitHubRepo = payload.Repository.Name,
                FailureTimestamp = payload.WorkflowRun.UpdatedAt ?? payload.WorkflowRun.CreatedAt,
                CommitLookbackDays = 7,
                SimilarFailuresCount = 5,
                MinSimilarity = 0.7f,
                EnablePatternDetection = true,
                EnableCommitAnalysis = true,
                Source = "GitHub Actions Webhook",
                Metadata = new Dictionary<string, string>
                {
                    ["WorkflowRunId"] = payload.WorkflowRun.Id.ToString(),
                    ["WorkflowName"] = payload.WorkflowRun.Name,
                    ["Branch"] = payload.WorkflowRun.HeadBranch,
                    ["CommitSha"] = payload.WorkflowRun.HeadSha,
                    ["WorkflowUrl"] = payload.WorkflowRun.HtmlUrl
                }
            };

            // Execute orchestration
            var result = await orchestrator.ExecuteAsync(request, CancellationToken.None);

            _logger.LogInformation(
                "Workflow failure analysis completed: OrchestrationId={OrchestrationId}, Status={Status}",
                result.OrchestrationId, result.Status);

            // Send notifications based on configuration
            await SendNotificationsAsync(payload, result, teamsService, githubCommentService, autoFixService);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing workflow failure for run {RunId}",
                payload.WorkflowRun?.Id);
        }
    }

    private async Task SendNotificationsAsync(
        GitHubWebhookPayload payload,
        OrchestrationResult result,
        TeamsNotificationService? teamsService,
        GitHubCommentService? githubCommentService,
        IAutoFixService? autoFixService)
    {
        try
        {
            // Skip notifications if confidence is below threshold
            if (result.Failure != null &&
                result.Failure.ConfidenceScore < _notificationSettings.MinConfidenceForNotification)
            {
                _logger.LogInformation(
                    "Skipping notifications due to low confidence: {Confidence}",
                    result.Failure.ConfidenceScore);
                return;
            }

            // Send Teams notification if configured
            if (!string.IsNullOrEmpty(_notificationSettings.TeamsWebhookUrl) && teamsService != null)
            {
                if (_notificationSettings.NotifyOnAllFailures ||
                    (_notificationSettings.NotifyOnNewPatterns && result.AssignedPattern != null))
                {
                    try
                    {
                        await teamsService.SendNotificationAsync(
                            _notificationSettings.TeamsWebhookUrl,
                            result);

                        _logger.LogInformation("Sent Teams notification for orchestration {OrchestrationId}",
                            result.OrchestrationId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send Teams notification");
                    }
                }
            }

            // Post GitHub PR comment if configured
            if (_notificationSettings.PostToGitHubPRs && githubCommentService != null &&
                payload.Repository != null)
            {
                // Try to find associated PR for the commit
                // For now, we'll skip this as it requires additional GitHub API calls
                // This can be enhanced later to find PRs by commit SHA
                _logger.LogInformation("GitHub PR comment posting is configured but PR detection not yet implemented");
            }

            // Create GitHub issue for new patterns if configured
            if (_notificationSettings.CreateGitHubIssuesForPatterns &&
                githubCommentService != null &&
                result.AssignedPattern != null &&
                payload.Repository != null)
            {
                try
                {
                    // Only create issue if this is a new pattern (high frequency = established pattern)
                    if (result.AssignedPattern.Frequency < 1.0) // Less than 1/day = new/rare pattern
                    {
                        await githubCommentService.CreateIssueForPatternAsync(
                            payload.Repository.Owner!.Login,
                            payload.Repository.Name,
                            result.AssignedPattern,
                            result);

                        _logger.LogInformation(
                            "Created GitHub issue for pattern '{PatternName}'",
                            result.AssignedPattern.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create GitHub issue for pattern");
                }
            }

            // Generate and apply auto-fix PR if configured
            if (_notificationSettings.CreateAutoFixPRs &&
                result.Failure != null &&
                payload.Repository != null &&
                autoFixService != null)
            {
                try
                {
                    // Check if confidence is high enough for auto-fix
                    if (result.Failure.ConfidenceScore >= _notificationSettings.MinConfidenceForAutoFixPR)
                    {
                        _logger.LogInformation(
                            "Attempting to generate auto-fix for orchestration {OrchestrationId}",
                            result.OrchestrationId);

                        var autoFix = await autoFixService.GenerateFixAsync(
                            result,
                            payload.Repository.Owner!.Login,
                            payload.Repository.Name);

                        if (autoFix.FixGenerated && autoFix.IsSafeToApply)
                        {
                            _logger.LogInformation(
                                "Auto-fix generated successfully. Creating PR...");

                            var prNumber = await autoFixService.ApplyFixAndCreatePRAsync(
                                autoFix,
                                payload.Repository.Owner!.Login,
                                payload.Repository.Name);

                            if (prNumber.HasValue)
                            {
                                _logger.LogInformation(
                                    "Created auto-fix PR #{PrNumber} for orchestration {OrchestrationId}",
                                    prNumber.Value,
                                    result.OrchestrationId);
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "Failed to create auto-fix PR for orchestration {OrchestrationId}",
                                    result.OrchestrationId);
                            }
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Auto-fix not generated or not safe to apply: {Reason}",
                                autoFix.Reason ?? "Unknown reason");
                        }
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Skipping auto-fix due to low confidence: {Confidence} < {Threshold}",
                            result.Failure.ConfidenceScore,
                            _notificationSettings.MinConfidenceForAutoFixPR);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating or applying auto-fix");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notifications");
        }
    }

    /// <summary>
    /// Test endpoint to verify webhook configuration
    /// </summary>
    [HttpGet("test")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult TestWebhook([FromServices] IPipelineOrchestrator? orchestrator,
        [FromServices] GitHubActionsLogService? logService,
        [FromServices] TeamsNotificationService? teamsService,
        [FromServices] GitHubCommentService? githubCommentService)
    {
        return Ok(new
        {
            Status = "Webhook endpoint is reachable",
            Timestamp = DateTime.UtcNow,
            SignatureVerificationEnabled = !string.IsNullOrEmpty(_gitHubSettings.WebhookSecret),
            OrchestratorConfigured = orchestrator != null,
            LogServiceConfigured = logService != null,
            TeamsNotificationConfigured = !string.IsNullOrEmpty(_notificationSettings.TeamsWebhookUrl),
            GitHubCommentServiceConfigured = githubCommentService != null
        });
    }
}

