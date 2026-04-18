using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticPipeline.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for managing failure analysis persistence
/// </summary>
public class FailureRepository : IFailureRepository
{
    private readonly AgenticPipelineDbContext _context;
    private readonly ILogger<FailureRepository> _logger;
    private readonly IEmbeddingService _embeddingService;

    public FailureRepository(
        AgenticPipelineDbContext context,
        ILogger<FailureRepository> logger,
        IEmbeddingService embeddingService)
    {
        _context = context;
        _logger = logger;
        _embeddingService = embeddingService;
    }

    public async Task<FailureEntity> SaveFailureAsync(FailureEntity failure, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving failure analysis to database. Error type: {ErrorType}", failure.ErrorType);

        // Generate embedding if not already set
        await GenerateEmbeddingIfNeededAsync(failure, cancellationToken);

        _context.Failures.Add(failure);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Failure analysis saved with ID: {Id}", failure.Id);
        return failure;
    }

    public async Task<FailureEntity> SaveFailureWithCommitsAsync(
        FailureEntity failure,
        List<CommitEntity> commits,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Saving failure analysis with {CommitCount} related commits. Error type: {ErrorType}",
            commits.Count, failure.ErrorType);

        // Generate embedding if not already set
        await GenerateEmbeddingIfNeededAsync(failure, cancellationToken);

        // Add failure
        _context.Failures.Add(failure);

        // Add related commits
        foreach (var commit in commits)
        {
            commit.Failure = failure;
            _context.Commits.Add(commit);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Failure analysis saved with ID: {Id} and {CommitCount} commits",
            failure.Id, commits.Count);

        return failure;
    }

    public async Task<FailureEntity?> GetByIdAsync(
        int id,
        bool includeCommits = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving failure analysis by ID: {Id}", id);

        var query = _context.Failures.AsQueryable();

        if (includeCommits)
        {
            query = query.Include(f => f.Commits);
        }

        var failure = await query.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (failure == null)
        {
            _logger.LogWarning("Failure analysis with ID {Id} not found", id);
        }
        else
        {
            _logger.LogInformation("Retrieved failure analysis with ID: {Id}", id);
        }

        return failure;
    }

    public async Task<List<FailureEntity>> GetHistoryAsync(
        int limit = 50,
        int offset = 0,
        string? errorType = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Retrieving failure history. Limit: {Limit}, Offset: {Offset}, ErrorType: {ErrorType}",
            limit, offset, errorType ?? "All");

        var query = _context.Failures
            .Include(f => f.Commits)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(errorType))
        {
            query = query.Where(f => f.ErrorType == errorType);
        }

        var failures = await query
            .OrderByDescending(f => f.AnalyzedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Retrieved {Count} failure analyses from history", failures.Count);
        return failures;
    }

    public async Task<int> GetCountAsync(string? errorType = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Failures.AsQueryable();

        if (!string.IsNullOrWhiteSpace(errorType))
        {
            query = query.Where(f => f.ErrorType == errorType);
        }

        var count = await query.CountAsync(cancellationToken);
        _logger.LogInformation("Total failures count: {Count} (ErrorType: {ErrorType})",
            count, errorType ?? "All");

        return count;
    }

    public async Task<FailureStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating failure statistics");

        var totalFailures = await _context.Failures.CountAsync(cancellationToken);

        if (totalFailures == 0)
        {
            _logger.LogInformation("No failures in database, returning empty statistics");
            return new FailureStatistics(
                TotalFailures: 0,
                AverageConfidence: 0,
                TotalInputTokens: 0,
                TotalOutputTokens: 0,
                FailuresByErrorType: new Dictionary<string, int>(),
                MostRecentAnalysis: null,
                OldestAnalysis: null);
        }

        var avgConfidence = await _context.Failures.AverageAsync(f => f.ConfidenceScore, cancellationToken);
        var totalInputTokens = await _context.Failures.SumAsync(f => f.InputTokens, cancellationToken);
        var totalOutputTokens = await _context.Failures.SumAsync(f => f.OutputTokens, cancellationToken);

        var failuresByType = await _context.Failures
            .GroupBy(f => f.ErrorType)
            .Select(g => new { ErrorType = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ErrorType, x => x.Count, cancellationToken);

        var mostRecent = await _context.Failures.MaxAsync(f => (DateTime?)f.AnalyzedAt, cancellationToken);
        var oldest = await _context.Failures.MinAsync(f => (DateTime?)f.AnalyzedAt, cancellationToken);

        var statistics = new FailureStatistics(
            TotalFailures: totalFailures,
            AverageConfidence: avgConfidence,
            TotalInputTokens: totalInputTokens,
            TotalOutputTokens: totalOutputTokens,
            FailuresByErrorType: failuresByType,
            MostRecentAnalysis: mostRecent,
            OldestAnalysis: oldest);

        _logger.LogInformation("Statistics calculated: {TotalFailures} total failures", totalFailures);
        return statistics;
    }

    /// <summary>
    /// Generates embedding for failure if not already set
    /// </summary>
    private async Task GenerateEmbeddingIfNeededAsync(FailureEntity failure, CancellationToken cancellationToken)
    {
        // Skip if embedding already exists
        if (failure.Embedding != null && failure.Embedding.Length > 0)
        {
            _logger.LogDebug("Embedding already exists for failure, skipping generation");
            return;
        }

        try
        {
            _logger.LogInformation("Generating embedding for failure. Error type: {ErrorType}", failure.ErrorType);

            // Create text representation for embedding
            // Combine error type, root cause, and original log content for semantic richness
            var textForEmbedding = $"{failure.ErrorType}: {failure.RootCause}\n\n{failure.LogContent}";

            // Generate embedding
            var embeddingVector = await _embeddingService.GenerateEmbeddingAsync(textForEmbedding, cancellationToken);

            // Convert float[] to byte[] for storage
            failure.Embedding = ConvertFloatArrayToBytes(embeddingVector);
            failure.EmbeddingDimension = embeddingVector.Length;

            _logger.LogInformation(
                "Embedding generated successfully. Dimensions: {Dimensions}, Storage size: {Bytes} bytes",
                embeddingVector.Length, failure.Embedding.Length);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the save operation
            // Embedding is optional - the failure analysis can still be saved without it
            _logger.LogError(ex,
                "Failed to generate embedding for failure. Error type: {ErrorType}. Continuing without embedding.",
                failure.ErrorType);
        }
    }

    /// <summary>
    /// Converts float array to byte array for database storage
    /// </summary>
    public static byte[] ConvertFloatArrayToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// Converts byte array back to float array for retrieval
    /// </summary>
    public static float[] ConvertBytesToFloatArray(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
