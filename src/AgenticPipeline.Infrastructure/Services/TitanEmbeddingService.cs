using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Implementation of embedding service using AWS Bedrock Titan Embeddings V2
/// </summary>
public class TitanEmbeddingService : IEmbeddingService
{
    private readonly AmazonBedrockRuntimeClient _bedrockClient;
    private readonly EmbeddingSettings _settings;
    private readonly ILogger<TitanEmbeddingService> _logger;

    public TitanEmbeddingService(
        IOptions<EmbeddingSettings> settings,
        ILogger<TitanEmbeddingService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        // Initialize Bedrock client with default credentials
        var region = RegionEndpoint.GetBySystemName(Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-west-2");
        _bedrockClient = new AmazonBedrockRuntimeClient(region);

        _logger.LogInformation(
            "TitanEmbeddingService initialized. Model: {Model}, Dimensions: {Dimensions}",
            _settings.ModelId, _settings.Dimensions);
    }

    /// <inheritdoc/>
    public int EmbeddingDimension => _settings.Dimensions;

    /// <inheritdoc/>
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        _logger.LogInformation("Generating embedding for text of length {Length} characters", text.Length);

        try
        {
            // Truncate text if too long
            var inputText = text.Length > _settings.MaxInputLength
                ? text.Substring(0, _settings.MaxInputLength)
                : text;

            if (text.Length > _settings.MaxInputLength)
            {
                _logger.LogWarning(
                    "Input text truncated from {Original} to {Max} characters",
                    text.Length, _settings.MaxInputLength);
            }

            // Build request body for Titan Embeddings V2
            var requestBody = new
            {
                inputText = inputText,
                dimensions = _settings.Dimensions,
                normalize = _settings.NormalizeEmbeddings
            };

            var requestJson = JsonSerializer.Serialize(requestBody);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);

            // Call Bedrock API
            var request = new InvokeModelRequest
            {
                ModelId = _settings.ModelId,
                ContentType = "application/json",
                Accept = "application/json",
                Body = new MemoryStream(requestBytes)
            };

            var startTime = DateTime.UtcNow;
            var response = await _bedrockClient.InvokeModelAsync(request, cancellationToken);
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation("Embedding generated successfully in {ElapsedMs}ms", elapsed);

            // Parse response
            using var reader = new StreamReader(response.Body);
            var responseJson = await reader.ReadToEndAsync(cancellationToken);

            _logger.LogDebug("Titan API Response: {Response}", responseJson);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var responseData = JsonSerializer.Deserialize<TitanEmbeddingResponse>(responseJson, options);

            if (responseData?.Embedding == null || responseData.Embedding.Length == 0)
            {
                _logger.LogError("Failed to parse embedding from response. Raw response: {Response}", responseJson);
                throw new InvalidOperationException("Received empty embedding from Titan API");
            }

            if (responseData.Embedding.Length != _settings.Dimensions)
            {
                throw new InvalidOperationException(
                    $"Expected embedding dimension {_settings.Dimensions} but received {responseData.Embedding.Length}");
            }

            return responseData.Embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding for text of length {Length}", text.Length);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<float[]>> GenerateEmbeddingsAsync(
        List<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts == null || texts.Count == 0)
        {
            throw new ArgumentException("Texts list cannot be null or empty", nameof(texts));
        }

        _logger.LogInformation("Generating embeddings for {Count} texts", texts.Count);

        var embeddings = new List<float[]>();
        var errors = 0;

        for (int i = 0; i < texts.Count; i++)
        {
            try
            {
                var embedding = await GenerateEmbeddingAsync(texts[i], cancellationToken);
                embeddings.Add(embedding);

                _logger.LogDebug("Generated embedding {Current}/{Total}", i + 1, texts.Count);
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Error generating embedding {Current}/{Total}", i + 1, texts.Count);

                // Add zero vector as placeholder for failed embeddings
                embeddings.Add(new float[_settings.Dimensions]);
            }
        }

        if (errors > 0)
        {
            _logger.LogWarning(
                "Completed batch embedding with {Errors} errors out of {Total} texts",
                errors, texts.Count);
        }
        else
        {
            _logger.LogInformation("Successfully generated {Count} embeddings", texts.Count);
        }

        return embeddings;
    }

    /// <summary>
    /// Response model for Titan Embeddings V2 API
    /// </summary>
    private class TitanEmbeddingResponse
    {
        public float[]? Embedding { get; set; }
        public int InputTextTokenCount { get; set; }
    }
}
