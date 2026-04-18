namespace AgenticPipeline.Api.Models;

/// <summary>
/// Request model for analyzing pipeline failures with GitHub commit context
/// </summary>
/// <param name="LogContent">Direct log content (use this OR LogFilePath, not both)</param>
/// <param name="LogFilePath">Path to log file on disk (use this OR LogContent, not both)</param>
/// <param name="Owner">GitHub repository owner (e.g., "anishsingh0302")</param>
/// <param name="Repo">GitHub repository name (e.g., "Semantic-Kernel")</param>
/// <param name="FailureTimestamp">Timestamp when the failure occurred (ISO 8601 format, optional - defaults to current time)</param>
/// <param name="LookbackHours">Hours before failure to search for commits (default: 24)</param>
public record AnalysisWithCommitsRequest(
    string? LogContent = null,
    string? LogFilePath = null,
    string? Owner = null,
    string? Repo = null,
    string? FailureTimestamp = null,
    int LookbackHours = 24);
