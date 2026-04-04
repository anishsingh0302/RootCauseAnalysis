using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using Microsoft.Extensions.Options;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// AWS Bedrock Claude service implementation
/// </summary>
public class ClaudeService : IClaudeService
{
    private readonly AmazonBedrockRuntimeClient _client;
    private readonly ClaudeSettings _settings;

    public ClaudeService(IOptions<ClaudeSettings> settings)
    {
        _settings = settings.Value;

        // Create Bedrock client with region from settings
        var regionEndpoint = RegionEndpoint.GetBySystemName(_settings.Region);
        _client = new AmazonBedrockRuntimeClient(regionEndpoint);
    }

    public async Task<string> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var request = new ClaudeRequest(
            Message: message,
            MaxTokens: _settings.MaxTokens,
            Temperature: _settings.Temperature,
            SystemPrompt: null);

        var response = await InvokeModelAsync(request, cancellationToken);
        return response.Content;
    }

    public async Task<ClaudeResponse> InvokeModelAsync(ClaudeRequest request, CancellationToken cancellationToken = default)
    {
        // Build request body in Claude's message format
        var messages = new[]
        {
            new
            {
                role = "user",
                content = request.Message
            }
        };

        var requestBody = new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = request.MaxTokens,
            messages = messages,
            temperature = request.Temperature,
            system = request.SystemPrompt
        };

        string requestJson = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        // Invoke Bedrock model
        var invokeRequest = new InvokeModelRequest
        {
            ModelId = _settings.ModelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(requestJson))
        };

        try
        {
            var invokeResponse = await _client.InvokeModelAsync(invokeRequest, cancellationToken);

            // Parse response
            using var reader = new StreamReader(invokeResponse.Body);
            string responseBody = await reader.ReadToEndAsync();

            var jsonDoc = JsonDocument.Parse(responseBody);
            var root = jsonDoc.RootElement;

            // Extract content
            string content = string.Empty;
            if (root.TryGetProperty("content", out var contentArray) && contentArray.GetArrayLength() > 0)
            {
                var firstContent = contentArray[0];
                if (firstContent.TryGetProperty("text", out var textElement))
                {
                    content = textElement.GetString() ?? string.Empty;
                }
            }

            // Extract token usage
            int inputTokens = 0;
            int outputTokens = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("input_tokens", out var inputTokensElement))
                    inputTokens = inputTokensElement.GetInt32();
                if (usage.TryGetProperty("output_tokens", out var outputTokensElement))
                    outputTokens = outputTokensElement.GetInt32();
            }

            return new ClaudeResponse(
                Content: content,
                InputTokens: inputTokens,
                OutputTokens: outputTokens,
                ModelId: _settings.ModelId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to invoke Claude model: {ex.Message}", ex);
        }
    }
}
