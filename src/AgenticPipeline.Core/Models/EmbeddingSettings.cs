namespace AgenticPipeline.Core.Models;

/// <summary>
/// Configuration settings for embedding generation service
/// </summary>
/// <param name="ModelId">AWS Bedrock model ID for embeddings (e.g., amazon.titan-embed-text-v2:0)</param>
/// <param name="Dimensions">Embedding vector dimensions (256, 512, or 1024)</param>
/// <param name="MaxInputLength">Maximum input text length in characters</param>
/// <param name="NormalizeEmbeddings">Whether to normalize embedding vectors</param>
public record EmbeddingSettings(
    string ModelId,
    int Dimensions,
    int MaxInputLength,
    bool NormalizeEmbeddings)
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Embedding";

    /// <summary>
    /// Default constructor with standard values for Titan Embeddings V2
    /// </summary>
    public EmbeddingSettings() : this(
        ModelId: "amazon.titan-embed-text-v2:0",
        Dimensions: 512,
        MaxInputLength: 8192,
        NormalizeEmbeddings: true)
    {
    }
}
