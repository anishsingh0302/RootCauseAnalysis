namespace AgenticPipeline.Core.Models;

/// <summary>
/// Configuration settings for notifications
/// </summary>
public record NotificationSettings
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// Base URL of the API for linking to reports (e.g., https://your-ngrok-url.ngrok.io)
    /// </summary>
    public string? ApiBaseUrl { get; init; }

    /// <summary>
    /// Microsoft Teams incoming webhook URL
    /// </summary>
    public string? TeamsWebhookUrl { get; init; }

    /// <summary>
    /// Slack incoming webhook URL
    /// </summary>
    public string? SlackWebhookUrl { get; init; }

    /// <summary>
    /// Whether to send notifications for all failures
    /// </summary>
    public bool NotifyOnAllFailures { get; init; } = false;

    /// <summary>
    /// Whether to send notifications only for new patterns
    /// </summary>
    public bool NotifyOnNewPatterns { get; init; } = true;

    /// <summary>
    /// Minimum confidence threshold to trigger notifications
    /// </summary>
    public double MinConfidenceForNotification { get; init; } = 0.8;

    /// <summary>
    /// Whether to post comments to GitHub PRs
    /// </summary>
    public bool PostToGitHubPRs { get; init; } = false;

    /// <summary>
    /// Whether to create GitHub issues for new patterns
    /// </summary>
    public bool CreateGitHubIssuesForPatterns { get; init; } = false;

    /// <summary>
    /// Whether to automatically create PRs with fixes when possible
    /// </summary>
    public bool CreateAutoFixPRs { get; init; } = false;

    /// <summary>
    /// Minimum confidence threshold for creating auto-fix PRs (0.0 to 1.0)
    /// </summary>
    public double MinConfidenceForAutoFixPR { get; init; } = 0.9;

    public NotificationSettings()
    {
    }
}
