using AgenticPipeline.Core.Models;

namespace AgenticPipeline.Core.Interfaces;

/// <summary>
/// Orchestrates the complete failure analysis pipeline
/// </summary>
public interface IPipelineOrchestrator
{
    /// <summary>
    /// Executes the full pipeline: RCA, similarity search, pattern detection, and commit analysis
    /// </summary>
    /// <param name="request">Orchestration request with log content and optional GitHub context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comprehensive orchestration result</returns>
    Task<OrchestrationResult> ExecuteAsync(OrchestrationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a running or completed orchestration
    /// </summary>
    /// <param name="orchestrationId">Orchestration ID</param>
    /// <returns>Orchestration result or null if not found</returns>
    Task<OrchestrationResult?> GetStatusAsync(Guid orchestrationId);
}
