namespace AgenticPipeline.Api.Models;

/// <summary>
/// Request model for pipeline failure analysis
/// </summary>
public class AnalysisRequest
{
    /// <summary>
    /// The CI/CD pipeline failure log content to analyze
    /// </summary>
    public string LogContent { get; set; } = string.Empty;
}
