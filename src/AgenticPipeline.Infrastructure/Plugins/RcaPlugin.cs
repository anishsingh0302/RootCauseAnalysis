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
        if (string.IsNullOrWhiteSpace(logContent))
        {
            throw new ArgumentException("Log content cannot be empty", nameof(logContent));
        }

        // Build the RCA prompt
        string systemPrompt = @"You are an expert CI/CD pipeline troubleshooting assistant.
Your task is to analyze build/test failure logs and provide detailed root cause analysis.

Analyze the provided log and respond with a JSON object following this exact structure:
{
  ""rootCause"": ""Brief 1-2 sentence summary of the root cause"",
  ""detailedAnalysis"": ""Detailed explanation of what went wrong and why"",
  ""affectedComponents"": [""list"", ""of"", ""affected"", ""files"", ""or"", ""components""],
  ""errorType"": ""One of: CompilationError, TestFailure, DependencyIssue, ConfigurationError, RuntimeError, NetworkError"",
  ""confidenceScore"": 0.95,
  ""recommendations"": [""Step 1: specific action"", ""Step 2: another action"", ""Step 3: verification step""]
}

Be specific, actionable, and confidence score should reflect certainty (0.0 to 1.0).
Return ONLY valid JSON, no additional text or markdown.";

        string userMessage = $@"Analyze this CI/CD pipeline failure log and provide root cause analysis:

```
{logContent}
```";

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
                AnalyzedAt: DateTime.UtcNow);

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
