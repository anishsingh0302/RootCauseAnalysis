using System.Text.Json.Serialization;

namespace AgenticPipeline.Api.Models;

/// <summary>
/// Azure DevOps Service Hook webhook payload for build.complete event
/// Documentation: https://learn.microsoft.com/en-us/azure/devops/service-hooks/events
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
    public string? EventType { get; set; }  // e.g., "build.complete"

    [JsonPropertyName("publisherId")]
    public string? PublisherId { get; set; }

    [JsonPropertyName("message")]
    public AzureDevOpsMessage? Message { get; set; }

    [JsonPropertyName("detailedMessage")]
    public AzureDevOpsDetailedMessage? DetailedMessage { get; set; }

    [JsonPropertyName("resource")]
    public AzureDevOpsBuildResource? Resource { get; set; }

    [JsonPropertyName("resourceVersion")]
    public string? ResourceVersion { get; set; }

    [JsonPropertyName("resourceContainers")]
    public AzureDevOpsResourceContainers? ResourceContainers { get; set; }

    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; }
}

public class AzureDevOpsMessage
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("html")]
    public string? Html { get; set; }

    [JsonPropertyName("markdown")]
    public string? Markdown { get; set; }
}

public class AzureDevOpsDetailedMessage
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("html")]
    public string? Html { get; set; }

    [JsonPropertyName("markdown")]
    public string? Markdown { get; set; }
}

public class AzureDevOpsBuildResource
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("buildNumber")]
    public string? BuildNumber { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("finishTime")]
    public DateTime? FinishTime { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }  // e.g., "completed"

    [JsonPropertyName("result")]
    public string? Result { get; set; }  // succeeded, failed, partiallySucceeded, canceled

    [JsonPropertyName("dropLocation")]
    public string? DropLocation { get; set; }

    [JsonPropertyName("drop")]
    public AzureDevOpsDrop? Drop { get; set; }

    [JsonPropertyName("log")]
    public AzureDevOpsLog? Log { get; set; }

    [JsonPropertyName("sourceGetVersion")]
    public string? SourceGetVersion { get; set; }

    [JsonPropertyName("lastChangedBy")]
    public AzureDevOpsIdentity? LastChangedBy { get; set; }

    [JsonPropertyName("retainIndefinitely")]
    public bool RetainIndefinitely { get; set; }

    [JsonPropertyName("hasDiagnostics")]
    public bool HasDiagnostics { get; set; }

    [JsonPropertyName("definition")]
    public AzureDevOpsBuildDefinition? Definition { get; set; }

    [JsonPropertyName("queue")]
    public AzureDevOpsQueue? Queue { get; set; }

    [JsonPropertyName("requests")]
    public List<AzureDevOpsBuildRequest>? Requests { get; set; }
}

public class AzureDevOpsDrop
{
    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }
}

public class AzureDevOpsLog
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }
}

public class AzureDevOpsIdentity
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("uniqueName")]
    public string? UniqueName { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }
}

public class AzureDevOpsBuildDefinition
{
    [JsonPropertyName("batchSize")]
    public int BatchSize { get; set; }

    [JsonPropertyName("triggerType")]
    public string? TriggerType { get; set; }

    [JsonPropertyName("definitionType")]
    public string? DefinitionType { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class AzureDevOpsQueue
{
    [JsonPropertyName("queueType")]
    public string? QueueType { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class AzureDevOpsBuildRequest
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("requestedFor")]
    public AzureDevOpsIdentity? RequestedFor { get; set; }
}

public class AzureDevOpsResourceContainers
{
    [JsonPropertyName("collection")]
    public AzureDevOpsResourceContainer? Collection { get; set; }

    [JsonPropertyName("account")]
    public AzureDevOpsResourceContainer? Account { get; set; }

    [JsonPropertyName("project")]
    public AzureDevOpsResourceContainer? Project { get; set; }
}

public class AzureDevOpsResourceContainer
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; set; }
}
