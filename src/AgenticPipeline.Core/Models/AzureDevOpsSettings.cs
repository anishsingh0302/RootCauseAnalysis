namespace AgenticPipeline.Core.Models;

/// <summary>
/// Configuration settings for Azure DevOps integration
/// </summary>
public record AzureDevOpsSettings
{
    public const string SectionName = "AzureDevOps";

    /// <summary>
    /// Azure DevOps organization name (e.g., "mycompany")
    /// </summary>
    public string? Organization { get; init; }

    /// <summary>
    /// Personal Access Token for Azure DevOps API
    /// Scopes needed: Build (Read), Code (Read)
    /// </summary>
    public string? PersonalAccessToken { get; init; }

    /// <summary>
    /// Webhook secret for signature verification (optional but recommended)
    /// </summary>
    public string? WebhookSecret { get; init; }

    /// <summary>
    /// Default project name if not specified in webhook
    /// </summary>
    public string? DefaultProject { get; init; }

    /// <summary>
    /// Azure DevOps REST API version
    /// </summary>
    public string ApiVersion { get; init; } = "7.1-preview.7";
}
