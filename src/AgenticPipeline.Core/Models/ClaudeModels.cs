namespace AgenticPipeline.Core.Models;

/// <summary>
/// Request model for Claude invocation
/// </summary>
/// <param name="Message">The user message to send to Claude</param>
/// <param name="MaxTokens">Maximum tokens for Claude's response</param>
/// <param name="Temperature">Temperature for response generation (0.0 to 1.0)</param>
/// <param name="SystemPrompt">Optional system prompt to guide Claude's behavior</param>
public record ClaudeRequest(
    string Message,
    int MaxTokens,
    double Temperature,
    string? SystemPrompt = null);

/// <summary>
/// Response model from Claude
/// </summary>
/// <param name="Content">The text content of Claude's response</param>
/// <param name="InputTokens">Number of input tokens used</param>
/// <param name="OutputTokens">Number of output tokens generated</param>
/// <param name="ModelId">The model ID that generated the response</param>
public record ClaudeResponse(
    string Content,
    int InputTokens,
    int OutputTokens,
    string ModelId)
{
    /// <summary>
    /// Total tokens used (input + output)
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;
}
