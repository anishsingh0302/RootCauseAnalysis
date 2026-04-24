using AgenticPipeline.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Service for sending failure analysis notifications to Microsoft Teams
/// </summary>
public class TeamsNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TeamsNotificationService> _logger;
    private readonly string? _apiBaseUrl;

    public TeamsNotificationService(
        HttpClient httpClient,
        ILogger<TeamsNotificationService> logger,
        IOptions<NotificationSettings> notificationSettings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiBaseUrl = notificationSettings.Value.ApiBaseUrl;
    }

    /// <summary>
    /// Sends orchestration results to a Microsoft Teams channel
    /// </summary>
    /// <param name="webhookUrl">Teams incoming webhook URL</param>
    /// <param name="result">Orchestration result to send</param>
    public async Task SendNotificationAsync(string webhookUrl, OrchestrationResult result)
    {
        try
        {
            _logger.LogInformation(
                "Sending failure analysis notification to Teams for orchestration {OrchestrationId}",
                result.OrchestrationId);

            var card = CreateAdaptiveCard(result);
            var json = JsonSerializer.Serialize(card);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(webhookUrl, content);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation(
                "Successfully sent Teams notification for orchestration {OrchestrationId}",
                result.OrchestrationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send Teams notification for orchestration {OrchestrationId}",
                result.OrchestrationId);
            throw;
        }
    }

    /// <summary>
    /// Sends a simple text message to Teams
    /// </summary>
    public async Task SendSimpleMessageAsync(string webhookUrl, string message)
    {
        try
        {
            var payload = new
            {
                text = message
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(webhookUrl, content);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Successfully sent simple Teams message");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send simple Teams message");
            throw;
        }
    }

    /// <summary>
    /// Creates an Adaptive Card for the orchestration result
    /// </summary>
    private object CreateAdaptiveCard(OrchestrationResult result)
    {
        if (result.Failure == null)
        {
            return new
            {
                type = "message",
                attachments = new[]
                {
                    new
                    {
                        contentType = "application/vnd.microsoft.card.adaptive",
                        content = new
                        {
                            type = "AdaptiveCard",
                            version = "1.4",
                            body = new[]
                            {
                                new
                                {
                                    type = "TextBlock",
                                    text = "WARNING: Failure Analysis Incomplete",
                                    size = "Large",
                                    weight = "Bolder"
                                }
                            }
                        }
                    }
                }
            };
        }

        // Determine color based on status
        var accentColor = result.Status switch
        {
            OrchestrationStatus.Completed => "Good",
            OrchestrationStatus.PartialSuccess => "Warning",
            OrchestrationStatus.Failed => "Attention",
            _ => "Default"
        };

        var statusEmoji = result.Status switch
        {
            OrchestrationStatus.Completed => "[OK]",
            OrchestrationStatus.PartialSuccess => "[WARN]",
            OrchestrationStatus.Failed => "[FAIL]",
            _ => "[RUNNING]"
        };

        var recommendations = JsonSerializer.Deserialize<List<string>>(
            result.Failure.RecommendationsJson) ?? new List<string>();

        var cardBody = new List<object>
        {
            // Title
            new
            {
                type = "TextBlock",
                text = "Automated Failure Analysis",
                size = "Large",
                weight = "Bolder",
                color = accentColor
            },
            // Status
            new
            {
                type = "TextBlock",
                text = $"{statusEmoji} **Status:** {result.Status}",
                wrap = true,
                spacing = "Small"
            },
            // Error Type
            new
            {
                type = "FactSet",
                facts = new[]
                {
                    new { title = "Error Type", value = result.Failure.ErrorType },
                    new { title = "Confidence", value = $"{result.Failure.ConfidenceScore:P0}" },
                    new { title = "Analyzed", value = result.StartedAt.ToString("yyyy-MM-dd HH:mm UTC") },
                    new { title = "Duration", value = $"{result.Duration?.TotalSeconds:F1}s" }
                }
            },
            // Root Cause Section
            new
            {
                type = "TextBlock",
                text = "Root Cause",
                weight = "Bolder",
                size = "Medium",
                spacing = "Medium"
            },
            new
            {
                type = "TextBlock",
                text = result.Failure.RootCause,
                wrap = true,
                spacing = "Small"
            }
        };

        // Add Recommendations
        if (recommendations.Count > 0)
        {
            cardBody.Add(new
            {
                type = "TextBlock",
                text = "Recommendations",
                weight = "Bolder",
                size = "Medium",
                spacing = "Medium"
            });

            foreach (var rec in recommendations.Take(3))
            {
                cardBody.Add(new
                {
                    type = "TextBlock",
                    text = $"• {rec}",
                    wrap = true,
                    spacing = "Small"
                });
            }
        }

        // Add Similar Failures Info
        if (result.SimilarFailures != null && result.SimilarFailures.Count > 0)
        {
            cardBody.Add(new
            {
                type = "TextBlock",
                text = $"Found {result.SimilarFailures.Count} similar past failure(s)",
                weight = "Bolder",
                spacing = "Medium"
            });

            foreach (var similar in result.SimilarFailures.Take(2))
            {
                cardBody.Add(new
                {
                    type = "TextBlock",
                    text = $"• **#{similar.FailureId}** ({similar.Similarity:P0} similar) - {similar.ErrorType}",
                    wrap = true,
                    spacing = "Small"
                });
            }
        }

        // Add Pattern Info
        if (result.AssignedPattern != null)
        {
            cardBody.Add(new
            {
                type = "TextBlock",
                text = "Pattern Recognition",
                weight = "Bolder",
                spacing = "Medium"
            });

            cardBody.Add(new
            {
                type = "TextBlock",
                text = $"Pattern: **{result.AssignedPattern.Name}** (Frequency: {result.AssignedPattern.Frequency:F1}/day)",
                wrap = true,
                spacing = "Small"
            });
        }

        // Add Commits Info
        if (result.RelatedCommits != null && result.RelatedCommits.Count > 0)
        {
            cardBody.Add(new
            {
                type = "TextBlock",
                text = $"{result.RelatedCommits.Count} recent commit(s) analyzed",
                spacing = "Medium"
            });
        }

        // Add Actions
        var actions = new List<object>();

        // Add view details action if we have orchestration ID and API base URL configured
        if (!string.IsNullOrEmpty(_apiBaseUrl))
        {
            actions.Add(new
            {
                type = "Action.OpenUrl",
                title = "View Full Report",
                url = $"{_apiBaseUrl.TrimEnd('/')}/api/pipeline/orchestrate/{result.OrchestrationId}/html"
            });
        }

        return new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = cardBody.ToArray(),
                        actions = actions.ToArray(),
                        msteams = new
                        {
                            width = "Full"
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Sends a notification for a new pattern detection
    /// </summary>
    public async Task SendPatternNotificationAsync(
        string webhookUrl,
        FailurePattern pattern,
        OrchestrationResult result)
    {
        try
        {
            _logger.LogInformation(
                "Sending pattern notification to Teams for pattern '{PatternName}'",
                pattern.Name);

            var card = CreatePatternCard(pattern, result);
            var json = JsonSerializer.Serialize(card);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(webhookUrl, content);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation(
                "Successfully sent Teams pattern notification for '{PatternName}'",
                pattern.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send Teams pattern notification for '{PatternName}'",
                pattern.Name);
            throw;
        }
    }

    /// <summary>
    /// Creates an Adaptive Card for pattern detection
    /// </summary>
    private object CreatePatternCard(FailurePattern pattern, OrchestrationResult result)
    {
        var cardBody = new List<object>
        {
            new
            {
                type = "TextBlock",
                text = "Failure Pattern Detected",
                size = "Large",
                weight = "Bolder",
                color = "Warning"
            },
            new
            {
                type = "TextBlock",
                text = $"**{pattern.Name}**",
                size = "Medium",
                weight = "Bolder",
                spacing = "Small"
            },
            new
            {
                type = "FactSet",
                facts = new[]
                {
                    new { title = "Error Type", value = pattern.PrimaryErrorType },
                    new { title = "Frequency", value = $"{pattern.Frequency:F2} failures/day" },
                    new { title = "Total Occurrences", value = pattern.FailureCount.ToString() },
                    new { title = "First Seen", value = pattern.FirstOccurrence.ToString("yyyy-MM-dd") },
                    new { title = "Last Seen", value = pattern.LastOccurrence.ToString("yyyy-MM-dd") }
                }
            }
        };

        if (pattern.CommonRootCauses.Count > 0)
        {
            cardBody.Add(new
            {
                type = "TextBlock",
                text = "Common Root Causes:",
                weight = "Bolder",
                spacing = "Medium"
            });

            foreach (var cause in pattern.CommonRootCauses.Take(3))
            {
                cardBody.Add(new
                {
                    type = "TextBlock",
                    text = $"• {cause}",
                    wrap = true,
                    spacing = "Small"
                });
            }
        }

        return new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = cardBody.ToArray(),
                        msteams = new
                        {
                            width = "Full"
                        }
                    }
                }
            }
        };
    }
}
