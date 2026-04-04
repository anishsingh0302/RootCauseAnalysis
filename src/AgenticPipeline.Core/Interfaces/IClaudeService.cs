using AgenticPipeline.Core.Models;

namespace AgenticPipeline.Core.Interfaces;

/// <summary>
/// Service interface for AWS Bedrock Claude integration
/// </summary>
public interface IClaudeService
{
    /// <summary>
    /// Sends a message to Claude and returns the response
    /// </summary>
    /// <param name="message">The message to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Claude's response text</returns>
    Task<string> SendMessageAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes Claude with a detailed request
    /// </summary>
    /// <param name="request">The Claude request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Claude's response</returns>
    Task<ClaudeResponse> InvokeModelAsync(ClaudeRequest request, CancellationToken cancellationToken = default);
}
