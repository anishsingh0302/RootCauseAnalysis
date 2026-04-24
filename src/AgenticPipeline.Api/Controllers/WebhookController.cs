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
    private readonly IPipelineOrchestrator? _orchestrator;
    private readonly GitHubActionsLogService? _logService;
    private readonly WebhookVerificationService _verificationService;
    private readonly GitHubSettings _gitHubSettings;
    private readonly NotificationSettings _notificationSettings;
    private readonly TeamsNotificationService? _teamsService;
    private readonly GitHubCommentService? _githubCommentService;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        WebhookVerificationService verificationService,
        IOptions<GitHubSettings> gitHubSettings,
        IOptions<NotificationSettings> notificationSettings,
        ILogger<WebhookController> logger,
        IPipelineOrchestrator? orchestrator = null,
        GitHubActionsLogService? logService = null,
        TeamsNotificationService? teamsService = null,
        GitHubCommentService? githubCommentService = null)
    {
        _verificationService = verificationService;
        _gitHubSettings = gitHubSettings.Value;
        _notificationSettings = notificationSettings.Value;
        _logger = logger;
        _orchestrator = orchestrator;
        _logService = logService;
        _teamsService = teamsService;
        _githubCommentService = githubCommentService;
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

            // Check if orchestrator is available
            if (_orchestrator == null || _logService == null)
            {
                _logger.LogWarning("Orchestrator or log service not configured");
                return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
                {
                    Title = "Feature Not Available",
                    Detail = "Webhook processing requires orchestrator and log service to be configured",
                    Status = StatusCodes.Status501NotImplemented
                });
            }

            // Process the webhook asynchronously (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessWorkflowFailureAsync(payload);
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

    private async Task ProcessWorkflowFailureAsync(GitHubWebhookPayload payload)
    {
        try
        {
            if (payload.WorkflowRun == null || payload.Repository == null || _orchestrator == null || _logService == null)
                return;

            _logger.LogInformation(
                "Processing workflow failure: RunId={RunId}, Repo={Repo}",
                payload.WorkflowRun.Id, payload.Repository.FullName);

            // Fetch workflow logs
            var logs = await _logService.FetchWorkflowLogsAsync(
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
            var result = await _orchestrator.ExecuteAsync(request, CancellationToken.None);

            _logger.LogInformation(
                "Workflow failure analysis completed: OrchestrationId={OrchestrationId}, Status={Status}",
                result.OrchestrationId, result.Status);

            // Send notifications based on configuration
            await SendNotificationsAsync(payload, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing workflow failure for run {RunId}",
                payload.WorkflowRun?.Id);
        }
    }

    private async Task SendNotificationsAsync(GitHubWebhookPayload payload, OrchestrationResult result)
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
            if (!string.IsNullOrEmpty(_notificationSettings.TeamsWebhookUrl) && _teamsService != null)
            {
                if (_notificationSettings.NotifyOnAllFailures ||
                    (_notificationSettings.NotifyOnNewPatterns && result.AssignedPattern != null))
                {
                    try
                    {
                        await _teamsService.SendNotificationAsync(
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
            if (_notificationSettings.PostToGitHubPRs && _githubCommentService != null &&
                payload.Repository != null)
            {
                // Try to find associated PR for the commit
                // For now, we'll skip this as it requires additional GitHub API calls
                // This can be enhanced later to find PRs by commit SHA
                _logger.LogInformation("GitHub PR comment posting is configured but PR detection not yet implemented");
            }

            // Create GitHub issue for new patterns if configured
            if (_notificationSettings.CreateGitHubIssuesForPatterns &&
                _githubCommentService != null &&
                result.AssignedPattern != null &&
                payload.Repository != null)
            {
                try
                {
                    // Only create issue if this is a new pattern (high frequency = established pattern)
                    if (result.AssignedPattern.Frequency < 1.0) // Less than 1/day = new/rare pattern
                    {
                        await _githubCommentService.CreateIssueForPatternAsync(
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
    public IActionResult TestWebhook()
    {
        return Ok(new
        {
            Status = "Webhook endpoint is reachable",
            Timestamp = DateTime.UtcNow,
            SignatureVerificationEnabled = !string.IsNullOrEmpty(_gitHubSettings.WebhookSecret),
            OrchestratorConfigured = _orchestrator != null,
            LogServiceConfigured = _logService != null,
            TeamsNotificationConfigured = !string.IsNullOrEmpty(_notificationSettings.TeamsWebhookUrl),
            GitHubCommentServiceConfigured = _githubCommentService != null
        });
    }
}

