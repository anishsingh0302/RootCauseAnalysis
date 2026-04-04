namespace AgenticPipeline.Api.Models;

/// <summary>
/// Request model for pipeline failure analysis
/// </summary>
/// <param name="LogContent">The CI/CD pipeline failure log content to analyze (optional if LogFilePath is provided)</param>
/// <param name="LogFilePath">Path to the log file to analyze (optional if LogContent is provided)</param>
public record AnalysisRequest(
    string? LogContent = null,
    string? LogFilePath = null);
