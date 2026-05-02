using System.ComponentModel;
using System.Text.Json;
using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using Microsoft.SemanticKernel;

namespace AgenticPipeline.Infrastructure.Plugins;

/// <summary>
/// Root Cause Analysis plugin for CI/CD pipeline failures
/// </summary>
public class RcaPlugin
{
    private readonly IClaudeService _claudeService;

    public RcaPlugin(IClaudeService claudeService)
    {
        _claudeService = claudeService;
    }

    [KernelFunction("analyze_failure")]
    [Description("Analyzes a CI/CD pipeline failure log and provides root cause analysis")]
    public async Task<string> AnalyzeFailureAsync(
        [Description("The pipeline failure log content")] string logContent,
        CancellationToken cancellationToken = default)
    {
        return await AnalyzeFailureWithCommitsAsync(logContent, null, cancellationToken);
    }

    /// <summary>
    /// Analyzes a pipeline failure with optional commit context for enhanced analysis
    /// </summary>
    [KernelFunction("analyze_failure_with_commits")]
    [Description("Analyzes a CI/CD pipeline failure log with commit context for enhanced root cause analysis")]
    public async Task<string> AnalyzeFailureWithCommitsAsync(
        [Description("The pipeline failure log content")] string logContent,
        [Description("Optional commit correlation JSON from CommitAnalyzerPlugin")] string? commitCorrelationJson = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(logContent))
        {
            throw new ArgumentException("Log content cannot be empty", nameof(logContent));
        }

        // Parse commit context if provided
        JsonElement? commitContext = null;
        if (!string.IsNullOrWhiteSpace(commitCorrelationJson))
        {
            try
            {
                commitContext = JsonSerializer.Deserialize<JsonElement>(commitCorrelationJson);
            }
            catch (JsonException)
            {
                // If commit JSON is invalid, proceed without it
                commitContext = null;
            }
        }

        // Build the RCA prompt
        string systemPrompt = @"You are an expert CI/CD pipeline troubleshooting assistant.
Your task is to analyze build/test failure logs and provide detailed root cause analysis.

Analyze the provided log and respond with a JSON object following this exact structure:
{
  ""rootCause"": ""Brief 1-2 sentence summary of the root cause"",
  ""detailedAnalysis"": ""Detailed explanation of what went wrong and why"",
  ""affectedComponents"": [""path/to/file1.cs"", ""path/to/file2.csproj""],
  ""errorType"": ""One of: CompilationError, TestFailure, DependencyIssue, ConfigurationError, RuntimeError, NetworkError"",
  ""confidenceScore"": 0.95,
  ""recommendations"": [""Step 1: specific action"", ""Step 2: another action"", ""Step 3: verification step""],
  ""suspectedCommits"": [
    {
      ""sha"": ""full commit SHA"",
      ""shortSha"": ""first 7 chars"",
      ""message"": ""commit message"",
      ""author"": ""author name"",
      ""commitDate"": ""ISO 8601 date or null if unknown"",
      ""confidence"": 0.0-1.0,
      ""reason"": ""why this commit is suspected"",
      ""suspectedFiles"": [""file1.cs"", ""file2.cs""],
      ""evidence"": ""evidence from failure log linking to this commit""
    }
  ]
}

CRITICAL REQUIREMENTS for affectedComponents:
- ONLY include actual FILE PATHS that exist in the source code repository (e.g., ""Customer.API/Services/CustomerRepository.cs"", ""Customer.API.Tests/BasicTests.cs"")
- DO NOT include build system step names (e.g., ""NuGet Restore step"", ""Build step"", ""Test step"")
- DO NOT include generic terms (e.g., ""database"", ""configuration"", ""build system"")
- Each component must be a valid file path with a file extension (.cs, .csproj, .json, .xml, etc.)
- If you see errors mentioning file paths and line numbers (e.g., ""at BasicTests.cs:line 23""), include those file paths
- Maximum 5 most relevant file paths

IMPORTANT: For commitDate, use valid ISO 8601 format (e.g., ""2026-04-21T10:30:00Z"") or null if the date is not available. DO NOT use strings like ""Unknown"".
If commit information is provided, analyze which commits are most likely responsible for the failure.
Be specific, actionable, and confidence score should reflect certainty (0.0 to 1.0).
Return ONLY valid JSON, no additional text or markdown.";

        // Build user message with optional commit context
        var userMessageBuilder = new System.Text.StringBuilder();
        userMessageBuilder.AppendLine("Analyze this CI/CD pipeline failure log and provide root cause analysis:");
        userMessageBuilder.AppendLine();
        userMessageBuilder.AppendLine("## Failure Log:");
        userMessageBuilder.AppendLine("```");
        userMessageBuilder.AppendLine(logContent);
        userMessageBuilder.AppendLine("```");

        // Add commit context if available
        if (commitContext.HasValue && commitContext.Value.TryGetProperty("aiAnalysis", out var aiAnalysis))
        {
            userMessageBuilder.AppendLine();
            userMessageBuilder.AppendLine("## Recent Commits Analysis:");
            userMessageBuilder.AppendLine("The following commits were made recently and may be related to this failure:");
            userMessageBuilder.AppendLine();
            userMessageBuilder.AppendLine(aiAnalysis.GetString() ?? "");
            userMessageBuilder.AppendLine();
            userMessageBuilder.AppendLine("Based on both the failure log and the commit analysis above, identify which commits are most likely responsible.");
        }

        string userMessage = userMessageBuilder.ToString();

        var request = new ClaudeRequest(
            Message: userMessage,
            MaxTokens: 2048,
            Temperature: 0.3, // Lower temperature for more focused analysis
            SystemPrompt: systemPrompt);

        var response = await _claudeService.InvokeModelAsync(request, cancellationToken);

        // Parse the JSON response into RcaResult
        try
        {
            // Strip markdown code fences if present (Claude often wraps JSON in ```json ... ```)
            var jsonContent = StripMarkdownCodeFences(response.Content);

            var rcaResult = JsonSerializer.Deserialize<RcaJsonResponse>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (rcaResult == null)
            {
                throw new InvalidOperationException("Failed to parse Claude's response");
            }

            // Extract commit correlation metadata if available
            CommitCorrelationMetadata? correlationMetadata = null;
            if (commitContext.HasValue &&
                commitContext.Value.TryGetProperty("repository", out var repo) &&
                commitContext.Value.TryGetProperty("commitsAnalyzed", out var commitsAnalyzed))
            {
                var timeRangeStart = DateTime.UtcNow.AddHours(-24);
                var timeRangeEnd = DateTime.UtcNow;
                var lookbackHours = 24;

                // Try to get time range from commit context
                if (commitContext.Value.TryGetProperty("commitContext", out var innerContext))
                {
                    if (innerContext.TryGetProperty("timeRange", out var timeRange))
                    {
                        if (timeRange.TryGetProperty("since", out var since))
                            DateTime.TryParse(since.GetString(), out timeRangeStart);
                        if (timeRange.TryGetProperty("until", out var until))
                            DateTime.TryParse(until.GetString(), out timeRangeEnd);
                    }
                }

                lookbackHours = (int)(timeRangeEnd - timeRangeStart).TotalHours;

                correlationMetadata = new CommitCorrelationMetadata(
                    Repository: repo.GetString() ?? "unknown",
                    TotalCommitsAnalyzed: commitsAnalyzed.GetInt32(),
                    TimeRangeStart: timeRangeStart,
                    TimeRangeEnd: timeRangeEnd,
                    LookbackHours: lookbackHours
                );
            }

            // Map to RcaResult model
            var result = new RcaResult(
                RootCause: rcaResult.RootCause,
                DetailedAnalysis: rcaResult.DetailedAnalysis,
                AffectedComponents: rcaResult.AffectedComponents ?? new List<string>(),
                ErrorType: rcaResult.ErrorType,
                ConfidenceScore: rcaResult.ConfidenceScore,
                Recommendations: rcaResult.Recommendations ?? new List<string>(),
                TokenUsage: new TokenUsage(
                    InputTokens: response.InputTokens,
                    OutputTokens: response.OutputTokens),
                AnalyzedAt: DateTime.UtcNow,
                SuspectedCommits: rcaResult.SuspectedCommits,
                CommitCorrelation: correlationMetadata);

            // Return as JSON string for Semantic Kernel
            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse RCA response: {ex.Message}. Response was: {response.Content}", ex);
        }
    }

    private static string StripMarkdownCodeFences(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        var trimmed = content.Trim();

        // Remove ```json or ``` prefix
        if (trimmed.StartsWith("```"))
        {
            var firstNewLine = trimmed.IndexOf('\n');
            if (firstNewLine >= 0)
                trimmed = trimmed[(firstNewLine + 1)..];
        }

        // Remove trailing ```
        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed[..^3];
        }

        return trimmed.Trim();
    }
}
