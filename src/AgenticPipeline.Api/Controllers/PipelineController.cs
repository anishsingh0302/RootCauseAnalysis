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
    private readonly ISimilarityService _similarityService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IPatternRecognitionService _patternRecognitionService;
    private readonly IPipelineOrchestrator? _pipelineOrchestrator;
    private readonly ILogger<PipelineController> _logger;

    public PipelineController(
        RcaPlugin rcaPlugin,
        CommitAnalyzerPlugin commitAnalyzerPlugin,
        IFailureRepository failureRepository,
        ISimilarityService similarityService,
        IEmbeddingService embeddingService,
        IPatternRecognitionService patternRecognitionService,
        ILogger<PipelineController> logger,
        IPipelineOrchestrator? pipelineOrchestrator = null)
    {
        _rcaPlugin = rcaPlugin;
        _commitAnalyzerPlugin = commitAnalyzerPlugin;
        _failureRepository = failureRepository;
        _similarityService = similarityService;
        _embeddingService = embeddingService;
        _patternRecognitionService = patternRecognitionService;
        _pipelineOrchestrator = pipelineOrchestrator;
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
    /// Searches for similar past failures using vector similarity
    /// </summary>
    /// <param name="request">Search request with query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ranked list of similar failures</returns>
    [HttpPost("search/similar")]
    [ProducesResponseType(typeof(SimilaritySearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SearchSimilarFailures(
        [FromBody] SimilaritySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation(
                "Similarity search requested. FailureId: {FailureId}, TopN: {TopN}, MinSimilarity: {MinSimilarity}",
                request.FailureId, request.TopN, request.MinSimilarity);

            // Validate request
            if (request.FailureId == null && string.IsNullOrWhiteSpace(request.FailureText))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "Either FailureId or FailureText must be provided",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (request.TopN <= 0 || request.TopN > 100)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid TopN",
                    Detail = "TopN must be between 1 and 100",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (request.MinSimilarity < 0.0f || request.MinSimilarity > 1.0f)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid MinSimilarity",
                    Detail = "MinSimilarity must be between 0.0 and 1.0",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Get candidates from database
            var candidateLimit = 1000; // Limit candidates for performance
            var candidates = await _failureRepository.GetHistoryAsync(
                limit: candidateLimit,
                offset: 0,
                errorType: request.ErrorType,
                cancellationToken);

            // Apply lookback filter if specified
            if (request.LookbackDays.HasValue)
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-request.LookbackDays.Value);
                candidates = candidates.Where(f => f.AnalyzedAt >= cutoffDate).ToList();
            }

            _logger.LogInformation("Retrieved {Count} candidate failures", candidates.Count);

            // Perform similarity search
            List<Core.Interfaces.SimilarityResult> results;
            QueryInfo queryInfo;

            if (request.FailureId.HasValue)
            {
                // Search by existing failure
                var queryFailure = await _failureRepository.GetByIdAsync(
                    request.FailureId.Value,
                    includeCommits: false,
                    cancellationToken);

                if (queryFailure == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Failure Not Found",
                        Detail = $"Failure with ID {request.FailureId.Value} not found",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                if (queryFailure.Embedding == null || queryFailure.Embedding.Length == 0)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "No Embedding",
                        Detail = $"Failure {request.FailureId.Value} does not have an embedding",
                        Status = StatusCodes.Status400BadRequest
                    });
                }

                results = _similarityService.FindSimilarFailures(
                    queryFailure,
                    candidates,
                    request.TopN,
                    request.MinSimilarity);

                queryInfo = new QueryInfo
                {
                    QueryType = "failure_id",
                    FailureId = queryFailure.Id,
                    ErrorType = queryFailure.ErrorType,
                    MinSimilarity = request.MinSimilarity,
                    TopN = request.TopN
                };
            }
            else
            {
                // Search by failure text - generate embedding on-the-fly
                var embedding = await _embeddingService.GenerateEmbeddingAsync(
                    request.FailureText!,
                    cancellationToken);

                results = _similarityService.FindSimilarFailures(
                    embedding,
                    candidates,
                    request.TopN,
                    request.MinSimilarity);

                queryInfo = new QueryInfo
                {
                    QueryType = "failure_text",
                    FailureId = null,
                    ErrorType = request.ErrorType,
                    MinSimilarity = request.MinSimilarity,
                    TopN = request.TopN
                };
            }

            var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            var response = new SimilaritySearchResponse
            {
                Query = queryInfo,
                Results = results.Select(r => new SimilarFailureResult
                {
                    Id = r.Failure.Id,
                    ErrorType = r.Failure.ErrorType,
                    RootCause = r.Failure.RootCause,
                    DetailedAnalysis = r.Failure.DetailedAnalysis,
                    AnalyzedAt = r.Failure.AnalyzedAt,
                    SimilarityScore = r.SimilarityScore,
                    ConfidenceScore = r.Failure.ConfidenceScore,
                    CommitCount = r.Failure.Commits?.Count ?? 0,
                    Repository = r.Failure.Repository
                }).ToList(),
                TotalCandidates = candidates.Count,
                ExecutionTimeMs = (long)executionTime
            };

            _logger.LogInformation(
                "Similarity search completed. Found {ResultCount} results in {ExecutionTimeMs}ms",
                response.ResultCount, response.ExecutionTimeMs);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing similarity search");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Similarity Search Error",
                Detail = $"An error occurred during similarity search: {ex.Message}",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Detects patterns in failure history using K-means clustering
    /// </summary>
    /// <param name="request">Pattern detection parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detected patterns with cluster information</returns>
    [HttpPost("patterns/detect")]
    [ProducesResponseType(typeof(PatternDetectionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DetectPatterns(
        [FromBody] PatternDetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Pattern detection requested. ClusterCount: {ClusterCount}, LookbackDays: {LookbackDays}",
                request.ClusterCount, request.LookbackDays);

            // Validate request
            if (request.LookbackDays <= 0 || request.LookbackDays > 365)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid LookbackDays",
                    Detail = "LookbackDays must be between 1 and 365",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (request.ClusterCount.HasValue && (request.ClusterCount <= 0 || request.ClusterCount > 50))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid ClusterCount",
                    Detail = "ClusterCount must be between 1 and 50",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Get recent failures
            var cutoffDate = DateTime.UtcNow.AddDays(-request.LookbackDays);
            var allFailures = await _failureRepository.GetHistoryAsync(
                limit: 10000, // Large limit to get all recent failures
                offset: 0,
                errorType: request.ErrorType,
                cancellationToken);

            var recentFailures = allFailures
                .Where(f => f.AnalyzedAt >= cutoffDate)
                .Where(f => f.Embedding != null && f.Embedding.Length > 0)
                .ToList();

            _logger.LogInformation(
                "Found {Count} failures with embeddings in last {Days} days",
                recentFailures.Count, request.LookbackDays);

            if (recentFailures.Count < 2)
            {
                return Ok(new PatternDetectionResult
                {
                    Patterns = new List<FailurePattern>(),
                    TotalFailures = recentFailures.Count,
                    ExecutionTimeMs = 0,
                    Parameters = new ClusteringParameters
                    {
                        ClusterCount = 0,
                        MaxIterations = request.MaxIterations,
                        MinClusterSize = request.MinClusterSize
                    }
                });
            }

            // Determine optimal cluster count if not specified
            int clusterCount;
            if (request.ClusterCount.HasValue)
            {
                clusterCount = request.ClusterCount.Value;
                _logger.LogInformation("Using specified cluster count: {K}", clusterCount);
            }
            else
            {
                _logger.LogInformation("Auto-detecting optimal cluster count...");
                clusterCount = _patternRecognitionService.DetermineOptimalClusterCount(
                    recentFailures,
                    maxClusters: Math.Min(10, recentFailures.Count / 3));
                _logger.LogInformation("Optimal cluster count determined: {K}", clusterCount);
            }

            // Run pattern detection
            var parameters = new ClusteringParameters
            {
                ClusterCount = clusterCount,
                MaxIterations = request.MaxIterations,
                MinClusterSize = request.MinClusterSize,
                RandomSeed = request.RandomSeed
            };

            var result = _patternRecognitionService.DetectPatterns(recentFailures, parameters);

            _logger.LogInformation(
                "Pattern detection completed. Found {PatternCount} patterns from {TotalFailures} failures in {ExecutionMs}ms",
                result.Patterns.Count, result.TotalFailures, result.ExecutionTimeMs);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting patterns");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Pattern Detection Error",
                Detail = $"An error occurred during pattern detection: {ex.Message}",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Orchestrates a complete pipeline analysis including RCA, similarity search, pattern detection, and commit analysis
    /// </summary>
    /// <param name="request">Orchestration request with log content and optional GitHub context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comprehensive analysis result</returns>
    [HttpPost("orchestrate")]
    [ProducesResponseType(typeof(OrchestrationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Orchestrate(
        [FromBody] OrchestrationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_pipelineOrchestrator == null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
            {
                Title = "Feature Not Available",
                Detail = "Pipeline orchestration is not configured. Please register IPipelineOrchestrator in Program.cs",
                Status = StatusCodes.Status501NotImplemented
            });
        }

        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.LogContent))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "LogContent is required",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (request.SimilarFailuresCount <= 0 || request.SimilarFailuresCount > 100)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid SimilarFailuresCount",
                    Detail = "SimilarFailuresCount must be between 1 and 100",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (request.MinSimilarity < 0.0f || request.MinSimilarity > 1.0f)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid MinSimilarity",
                    Detail = "MinSimilarity must be between 0.0 and 1.0",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation(
                "Orchestration requested. LogLength: {LogLength}, EnableCommitAnalysis: {EnableCommitAnalysis}, EnablePatternDetection: {EnablePatternDetection}",
                request.LogContent.Length, request.EnableCommitAnalysis, request.EnablePatternDetection);

            // Execute orchestration
            var result = await _pipelineOrchestrator.ExecuteAsync(request, cancellationToken);

            _logger.LogInformation(
                "Orchestration {OrchestrationId} completed with status {Status} in {Duration}s",
                result.OrchestrationId, result.Status, result.Duration?.TotalSeconds);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid orchestration request: {Message}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during pipeline orchestration");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Orchestration Error",
                Detail = $"An error occurred during pipeline orchestration: {ex.Message}",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Gets the status of an orchestration run
    /// </summary>
    /// <param name="orchestrationId">The orchestration ID</param>
    /// <returns>Orchestration status and results</returns>
    [HttpGet("orchestrate/{orchestrationId}")]
    [ProducesResponseType(typeof(OrchestrationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> GetOrchestrationStatus([FromRoute] Guid orchestrationId)
    {
        if (_pipelineOrchestrator == null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
            {
                Title = "Feature Not Available",
                Detail = "Pipeline orchestration is not configured",
                Status = StatusCodes.Status501NotImplemented
            });
        }

        try
        {
            var result = await _pipelineOrchestrator.GetStatusAsync(orchestrationId);

            if (result == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Not Found",
                    Detail = $"Orchestration with ID {orchestrationId} was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orchestration status for {OrchestrationId}", orchestrationId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Retrieval Error",
                Detail = $"An error occurred while retrieving orchestration status: {ex.Message}",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get orchestration status as HTML report
    /// </summary>
    [HttpGet("orchestrate/{orchestrationId}/html")]
    [Produces("text/html")]
    public async Task<IActionResult> GetOrchestrationHtmlReport([FromRoute] Guid orchestrationId)
    {
        if (_pipelineOrchestrator == null)
        {
            return Content("<html><body><h1>Feature Not Available</h1><p>Pipeline orchestration is not configured</p></body></html>", "text/html");
        }

        try
        {
            var result = await _pipelineOrchestrator.GetStatusAsync(orchestrationId);

            if (result == null)
            {
                return Content($"<html><body><h1>Not Found</h1><p>Orchestration with ID {orchestrationId} was not found</p></body></html>", "text/html");
            }

            // Read the HTML template
            var templatePath = Path.Combine(AppContext.BaseDirectory, "Views", "OrchestrationReport.html");
            if (!System.IO.File.Exists(templatePath))
            {
                return Content("<html><body><h1>Error</h1><p>HTML template not found</p></body></html>", "text/html");
            }

            var template = await System.IO.File.ReadAllTextAsync(templatePath);

            // Simple template replacement
            var html = template
                .Replace("{{OrchestrationId}}", orchestrationId.ToString())
                .Replace("{{AnalyzedAt}}", result.StartedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"))
                .Replace("{{Duration}}", result.Duration?.ToString(@"hh\:mm\:ss") ?? "N/A")
                .Replace("{{Status}}", result.Status.ToString())
                .Replace("{{StatusClass}}", result.Status.ToString().ToLower())
                .Replace("{{Confidence}}", result.Failure?.ConfidenceScore.ToString("P0") ?? "N/A")
                .Replace("{{ErrorType}}", result.Failure?.ErrorType ?? "Unknown")
                .Replace("{{RootCause}}", result.Failure?.RootCause ?? "No analysis available")
                .Replace("{{DetailedAnalysis}}", result.Failure?.DetailedAnalysis ?? "No detailed analysis available")
                .Replace("{{AffectedComponents}}", string.Join("", System.Text.Json.JsonSerializer.Deserialize<List<string>>(result.Failure?.AffectedComponentsJson ?? "[]")?.Select(c => $"<span class='component-tag'>{c}</span>") ?? Array.Empty<string>()))
                .Replace("{{Recommendations}}", string.Join("", System.Text.Json.JsonSerializer.Deserialize<List<string>>(result.Failure?.RecommendationsJson ?? "[]")?.Select((r, i) => $"<div class='recommendation-item'><strong>{i + 1}.</strong> {r}</div>") ?? Array.Empty<string>()))
                .Replace("{{SimilarFailureCount}}", result.SimilarFailures?.Count.ToString() ?? "0")
                .Replace("{{SimilarFailures}}", string.Join("", result.SimilarFailures?.Select(sf =>
                    $"<div class='similar-failure'><strong>Failure #{sf.FailureId}</strong> <span class='similarity-score'>{sf.Similarity:P0} similar</span><br><strong>{sf.ErrorType}</strong><br>{sf.RootCause}</div>") ?? Array.Empty<string>()))
                .Replace("{{CommitCount}}", result.RelatedCommits?.Count.ToString() ?? "0")
                .Replace("{{Commits}}", string.Join("", result.RelatedCommits?.Take(5).Select(c =>
                    $"<div class='commit-item'><span class='commit-sha'>{c.Sha[..7]}</span> by {c.Author}<br>{c.Message}<br><small>{c.CommitDate:yyyy-MM-dd HH:mm}</small></div>") ?? Array.Empty<string>()))
                .Replace("{{PatternInfo}}", result.AssignedPattern != null ?
                    $"<div class='pattern-info'><strong>Pattern:</strong> {result.AssignedPattern.Name}<br><strong>Frequency:</strong> {result.AssignedPattern.Frequency:F1} failures/day<br><strong>Total Occurrences:</strong> {result.AssignedPattern.FailureCount}<br><strong>Similarity:</strong> {result.PatternSimilarity:P0}</div>" : "")
                .Replace("{{TotalSteps}}", result.ExecutionSteps?.Count.ToString() ?? "0")
                .Replace("{{TotalTokens}}", (result.TotalInputTokens + result.TotalOutputTokens).ToString())
                .Replace("{{ExecutionSteps}}", string.Join("", result.ExecutionSteps?.Select(step =>
                    $"<div class='execution-step'><div class='step-icon step-{(step.Success ? "success" : "failed")}'>{(step.Success ? "✓" : "✗")}</div><div class='step-info'><div class='step-name'>{step.StepName}</div><div class='step-duration'>{step.Duration?.TotalSeconds.ToString("F1") ?? "0.0"}s</div></div></div>") ?? Array.Empty<string>()));

            // Handle conditional sections
            html = html.Replace("{{#if HasSimilarFailures}}", result.SimilarFailures?.Count > 0 ? "" : "<!--")
                       .Replace("{{/if}}", result.SimilarFailures?.Count > 0 ? "" : "-->");

            html = html.Replace("{{#if HasPattern}}", result.AssignedPattern != null ? "" : "<!--");
            html = html.Replace("{{#if HasCommits}}", result.RelatedCommits?.Count > 0 ? "" : "<!--");

            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orchestration HTML report for {OrchestrationId}", orchestrationId);
            return Content($"<html><body><h1>Error</h1><p>{ex.Message}</p></body></html>", "text/html");
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
