using AgenticPipeline.Core.Models;

namespace AgenticPipeline.Core.Interfaces;

/// <summary>
/// Repository interface for managing failure analysis persistence
/// </summary>
public interface IFailureRepository
{
    /// <summary>
    /// Saves a failure analysis to the database
    /// </summary>
    /// <param name="failure">The failure entity to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The saved failure entity with generated ID</returns>
    Task<FailureEntity> SaveFailureAsync(FailureEntity failure, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a failure analysis with related commits
    /// </summary>
    /// <param name="failure">The failure entity to save</param>
    /// <param name="commits">List of related commit entities</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The saved failure entity with generated ID</returns>
    Task<FailureEntity> SaveFailureWithCommitsAsync(
        FailureEntity failure,
        List<CommitEntity> commits,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a failure analysis by ID
    /// </summary>
    /// <param name="id">The failure ID</param>
    /// <param name="includeCommits">Whether to include related commits</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The failure entity or null if not found</returns>
    Task<FailureEntity?> GetByIdAsync(int id, bool includeCommits = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all failure analyses with optional filtering
    /// </summary>
    /// <param name="limit">Maximum number of records to return (default: 50)</param>
    /// <param name="offset">Number of records to skip (default: 0)</param>
    /// <param name="errorType">Optional filter by error type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of failure entities ordered by AnalyzedAt descending</returns>
    Task<List<FailureEntity>> GetHistoryAsync(
        int limit = 50,
        int offset = 0,
        string? errorType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of failure analyses
    /// </summary>
    /// <param name="errorType">Optional filter by error type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total count of failures</returns>
    Task<int> GetCountAsync(string? errorType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about failure analyses
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Statistics object</returns>
    Task<FailureStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}
