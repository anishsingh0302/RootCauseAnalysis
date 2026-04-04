using AgenticPipeline.Api.Models;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Plugins;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AgenticPipeline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PipelineController : ControllerBase
{
    private readonly RcaPlugin _rcaPlugin;
    private readonly ILogger<PipelineController> _logger;

    public PipelineController(RcaPlugin rcaPlugin, ILogger<PipelineController> logger)
    {
        _rcaPlugin = rcaPlugin;
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
