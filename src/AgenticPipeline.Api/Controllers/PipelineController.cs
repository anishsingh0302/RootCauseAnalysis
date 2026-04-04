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
            _logger.LogInformation("Received pipeline analysis request. Log length: {LogLength} characters",
                request.LogContent?.Length ?? 0);

            if (string.IsNullOrWhiteSpace(request.LogContent))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "Log content cannot be empty",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Call RCA plugin
            var resultJson = await _rcaPlugin.AnalyzeFailureAsync(request.LogContent, cancellationToken);

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
