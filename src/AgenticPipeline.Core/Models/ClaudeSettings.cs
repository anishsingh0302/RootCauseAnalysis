namespace AgenticPipeline.Core.Models;

/// <summary>
/// Configuration settings for Claude service
/// </summary>
/// <param name="ModelId">AWS Bedrock model ID (e.g., us.anthropic.claude-sonnet-4-6)</param>
/// <param name="Region">AWS region (e.g., us-west-2)</param>
/// <param name="MaxTokens">Maximum tokens for Claude's response</param>
/// <param name="Temperature">Temperature for response generation (0.0 to 1.0) - Higher values = more creative, lower values = more focused</param>
public record ClaudeSettings(
    string ModelId,
    string Region,
    int MaxTokens,
    double Temperature)
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Claude";

    /// <summary>
    /// Default constructor with standard values
    /// </summary>
    public ClaudeSettings() : this(
        ModelId: "us.anthropic.claude-sonnet-4-6",
        Region: "us-west-2",
        MaxTokens: 2048,
        Temperature: 0.7)
    {
    }
}
