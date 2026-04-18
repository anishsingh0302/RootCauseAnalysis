using AgenticPipeline.Api.Models;
using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Extensions;
using AgenticPipeline.Infrastructure.Plugins;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AgenticPipeline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PipelineController : ControllerBase
{
    private readonly RcaPlugin _rcaPlugin;
    private readonly CommitAnalyzerPlugin _commitAnalyzerPlugin;
    private readonly IFailureRepository _failureRepository;
    private readonly ILogger<PipelineController> _logger;

    public PipelineController(
        RcaPlugin rcaPlugin,
        CommitAnalyzerPlugin commitAnalyzerPlugin,
        IFailureRepository failureRepository,
        ILogger<PipelineController> logger)
    {
        _rcaPlugin = rcaPlugin;
        _commitAnalyzerPlugin = commitAnalyzerPlugin;
        _failureRepository = failureRepository;
        _logger = logger;
    }

    /// <summary>
    /// Analyzes a CI/CD pipeline failure log and returns root cause analysis
    /// </summary>
    /// <param name="request">The analysis request containing the log content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Root cause analysis result</returns>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(RcaResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AnalyzePipeline(
        [FromBody] AnalysisRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var (logContent, errorResult) = await ResolveLogContentAsync(request, cancellationToken);
            if (errorResult != null)
                return errorResult;

            // Call RCA plugin
            var resultJson = await _rcaPlugin.AnalyzeFailureAsync(logContent: logContent!, cancellationToken);

            // Parse the JSON result
            var result = JsonSerializer.Deserialize<RcaResult>(resultJson);

            if (result == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Analysis Failed",
                    Detail = "Failed to parse analysis result",
                    Status = StatusCodes.Status500InternalServerError
                });
            }

            _logger.LogInformation("Analysis completed successfully. Error type: {ErrorType}, Confidence: {Confidence}",
                result.ErrorType, result.ConfidenceScore);

            // Save to database
            var failureEntity = result.ToEntity(logContent!);
            var savedEntity = await _failureRepository.SaveFailureAsync(failureEntity, cancellationToken);

            _logger.LogInformation("Analysis result saved to database with ID: {Id}", savedEntity.Id);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request: {Message}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing pipeline failure");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Analysis Error",
                Detail = $"An error occurred while analyzing the pipeline: {ex.Message}",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    private async Task<(string? LogContent, IActionResult? Error)> ResolveLogContentAsync(
        AnalysisRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.LogFilePath))
        {
            return await ReadLogFromFileAsync(request.LogFilePath, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.LogContent))
        {
            _logger.LogInformation("Received pipeline analysis request with direct content. Log length: {LogLength} characters", request.LogContent.Length);
            return (request.LogContent, null);
        }

        return (null, BadRequest(new ProblemDetails
        {
            Title = "Invalid Request",
            Detail = "Either 'logContent' or 'logFilePath' must be provided",
            Status = StatusCodes.Status400BadRequest
        }));
    }

    private async Task<(string? LogContent, IActionResult? Error)> ReadLogFromFileAsync(
        string filePath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received pipeline analysis request with file path: {FilePath}", filePath);

        if (!System.IO.File.Exists(filePath))
        {
            return (null, BadRequest(new ProblemDetails
            {
                Title = "File Not Found",
                Detail = $"The log file '{filePath}' does not exist or cannot be accessed",
                Status = StatusCodes.Status400BadRequest
            }));
        }

        try
        {
            var content = await System.IO.File.ReadAllTextAsync(filePath, cancellationToken);
            _logger.LogInformation("Successfully read log file. Length: {LogLength} characters", content.Length);

            if (string.IsNullOrWhiteSpace(content))
            {
                return (null, BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "Log content is empty",
                    Status = StatusCodes.Status400BadRequest
                }));
            }

            return (content, null);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to file: {FilePath}", filePath);
            return (null, StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Access Denied",
                Detail = $"Access denied to read the file '{filePath}'",
                Status = StatusCodes.Status403Forbidden
            }));
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error reading file: {FilePath}", filePath);
            return (null, StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "File Read Error",
                Detail = $"Error reading the file: {ex.Message}",
                Status = StatusCodes.Status500InternalServerError
            }));
        }
    }

    /// <summary>
    /// Analyzes a CI/CD pipeline failure with GitHub commit context for enhanced analysis
    /// </summary>
    /// <param name="request">The analysis request with GitHub repository information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Enhanced root cause analysis with suspected commits</returns>
    [HttpPost("analyze-with-commits")]
    [ProducesResponseType(typeof(RcaResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AnalyzeWithCommits(
        [FromBody] AnalysisWithCommitsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Resolve log content
            var analysisRequest = new AnalysisRequest
            {
                LogContent = request.LogContent,
                LogFilePath = request.LogFilePath
            };

            var (logContent, errorResult) = await ResolveLogContentAsync(analysisRequest, cancellationToken);
            if (errorResult != null)
                return errorResult;

            // Step 2: Validate GitHub parameters
            if (string.IsNullOrWhiteSpace(request.Owner) || string.IsNullOrWhiteSpace(request.Repo))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "Both 'owner' and 'repo' must be provided for commit analysis",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Step 3: Prepare timestamp
            var timestamp = string.IsNullOrWhiteSpace(request.FailureTimestamp)
                ? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                : request.FailureTimestamp;

            _logger.LogInformation(
                "Starting enhanced analysis with commits for {Owner}/{Repo}, lookback: {Hours}h",
                request.Owner, request.Repo, request.LookbackHours);

            // Step 4: Correlate commits with failure using AI
            var commitCorrelationJson = await _commitAnalyzerPlugin.CorrelateCommitsWithFailure(
                request.Owner,
                request.Repo,
                logContent!,
                timestamp,
                request.LookbackHours);

            _logger.LogInformation("Commit correlation completed");

            // Step 5: Perform enhanced RCA with commit context
            var resultJson = await _rcaPlugin.AnalyzeFailureWithCommitsAsync(
                logContent!,
                commitCorrelationJson,
                cancellationToken);

            // Parse the JSON result
            var result = JsonSerializer.Deserialize<RcaResult>(resultJson);

            if (result == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Analysis Failed",
                    Detail = "Failed to parse analysis result",
                    Status = StatusCodes.Status500InternalServerError
                });
            }

            _logger.LogInformation(
                "Enhanced analysis completed. Error type: {ErrorType}, Confidence: {Confidence}, Suspected commits: {CommitCount}",
                result.ErrorType, result.ConfidenceScore, result.SuspectedCommits?.Count ?? 0);

            // Save to database with commits
            var failureEntity = result.ToEntity(logContent!);
            var commitEntities = result.SuspectedCommits?
                .Select(sc => sc.ToEntity())
                .ToList() ?? new List<CommitEntity>();

            var savedEntity = await _failureRepository.SaveFailureWithCommitsAsync(
                failureEntity,
                commitEntities,
                cancellationToken);

            _logger.LogInformation(
                "Analysis result saved to database with ID: {Id} and {CommitCount} commits",
                savedEntity.Id, commitEntities.Count);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request: {Message}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing pipeline failure with commits");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Analysis Error",
                Detail = $"An error occurred while analyzing the pipeline: {ex.Message}",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Retrieves failure analysis history with optional filtering and pagination
    /// </summary>
    /// <param name="limit">Maximum number of records to return (default: 50, max: 100)</param>
    /// <param name="offset">Number of records to skip for pagination (default: 0)</param>
    /// <param name="errorType">Optional filter by error type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of past failure analyses ordered by most recent first</returns>
    [HttpGet("history")]
    [ProducesResponseType(typeof(List<FailureEntity>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        [FromQuery] string? errorType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (limit <= 0 || limit > 100)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "Limit must be between 1 and 100",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (offset < 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "Offset must be non-negative",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation(
                "Retrieving failure history. Limit: {Limit}, Offset: {Offset}, ErrorType: {ErrorType}",
                limit, offset, errorType ?? "All");

            var history = await _failureRepository.GetHistoryAsync(
                limit,
                offset,
                errorType,
                cancellationToken);

            _logger.LogInformation("Retrieved {Count} failure analyses from history", history.Count);

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving failure history");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Retrieval Error",
                Detail = $"An error occurred while retrieving failure history: {ex.Message}",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Retrieves a specific failure analysis by ID
    /// </summary>
    /// <param name="id">The failure analysis ID</param>
    /// <param name="includeCommits">Whether to include related commits (default: true)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The failure analysis with optional related commits</returns>
    [HttpGet("history/{id}")]
    [ProducesResponseType(typeof(FailureEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(
        [FromRoute] int id,
        [FromQuery] bool includeCommits = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving failure analysis by ID: {Id}", id);

            var failure = await _failureRepository.GetByIdAsync(id, includeCommits, cancellationToken);

            if (failure == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Not Found",
                    Detail = $"Failure analysis with ID {id} was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            _logger.LogInformation(
                "Retrieved failure analysis {Id} with {CommitCount} commits",
                id, failure.Commits.Count);

            return Ok(failure);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving failure analysis by ID: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Retrieval Error",
                Detail = $"An error occurred while retrieving the failure analysis: {ex.Message}",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Retrieves statistics about all failure analyses
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregate statistics including counts, averages, and token usage</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(FailureStatistics), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStatistics(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calculating failure analysis statistics");

            var statistics = await _failureRepository.GetStatisticsAsync(cancellationToken);

            _logger.LogInformation(
                "Statistics calculated: {TotalFailures} total failures, {AvgConfidence:P0} avg confidence",
                statistics.TotalFailures, statistics.AverageConfidence);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating statistics");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Statistics Error",
                Detail = $"An error occurred while calculating statistics: {ex.Message}",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Service = "Agentic Pipeline Troubleshooter"
        });
    }
}
