using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Service for extracting detailed error information from failure logs
/// </summary>
public class ErrorExtractionService
{
    private readonly IClaudeService _claudeService;
    private readonly ILogger<ErrorExtractionService> _logger;

    public ErrorExtractionService(
        IClaudeService claudeService,
        ILogger<ErrorExtractionService> logger)
    {
        _claudeService = claudeService;
        _logger = logger;
    }

    /// <summary>
    /// Extracts detailed error analysis from failure logs
    /// </summary>
    public async Task<ErrorAnalysis> ExtractErrorDetailsAsync(
        string logContent,
        FailureEntity? failureEntity = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Extracting detailed error information from logs");

            var extractionPrompt = BuildExtractionPrompt(logContent, failureEntity);

            var response = await _claudeService.SendMessageAsync(
                extractionPrompt,
                cancellationToken: cancellationToken);

            var errorAnalysis = ParseErrorAnalysis(response);

            _logger.LogInformation(
                "Error extraction complete: Type={ErrorType}, File={FileName}, Line={LineNumber}, Confidence={Confidence}",
                errorAnalysis.ErrorType,
                errorAnalysis.FileName,
                errorAnalysis.LineNumber,
                errorAnalysis.ExtractionConfidence);

            return errorAnalysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during error extraction");
            return new ErrorAnalysis
            {
                ErrorType = "Unknown",
                ErrorMessage = "Failed to extract error details",
                ExtractionConfidence = 0.0
            };
        }
    }

    private string BuildExtractionPrompt(string logContent, FailureEntity? failureEntity)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are an expert at analyzing build/test failure logs.");
        prompt.AppendLine("Extract SPECIFIC, ACTIONABLE error details from the log.");
        prompt.AppendLine();

        prompt.AppendLine("## Failure Log");
        prompt.AppendLine("```");
        prompt.AppendLine(logContent);
        prompt.AppendLine("```");
        prompt.AppendLine();

        if (failureEntity != null)
        {
            prompt.AppendLine("## Previous RCA Analysis");
            prompt.AppendLine($"Root Cause: {failureEntity.RootCause}");
            prompt.AppendLine($"Error Type: {failureEntity.ErrorType}");
            prompt.AppendLine();
        }

        prompt.AppendLine("## Task");
        prompt.AppendLine("Extract the EXACT error details for the PRIMARY error ONLY:");
        prompt.AppendLine();
        prompt.AppendLine("**CRITICAL: SINGLE ERROR FOCUS**");
        prompt.AppendLine("- Identify THE PRIMARY error that caused the build/test to fail");
        prompt.AppendLine("- IGNORE all secondary errors, warnings, and informational messages");
        prompt.AppendLine("- IGNORE configuration warnings unless that's the PRIMARY failure");
        prompt.AppendLine("- For test failures: Extract the FIRST test that failed (not all failing tests)");
        prompt.AppendLine("- For compilation errors: Extract the FIRST error (not all compiler errors)");
        prompt.AppendLine();
        prompt.AppendLine("**How to identify PRIMARY error:**");
        prompt.AppendLine("1. Look for 'Failed!' or '[FAIL]' markers (test failures)");
        prompt.AppendLine("2. Look for 'error CS...' or 'error MSB...' (compilation errors)");
        prompt.AppendLine("3. Look for stack traces starting with 'at' (runtime errors)");
        prompt.AppendLine("4. The PRIMARY error is usually near the END of the log (but before summary)");
        prompt.AppendLine("5. Warnings (MSB30XX, CS01XX) are NOT primary errors unless build completely failed due to them");
        prompt.AppendLine();
        prompt.AppendLine("**Extract these details for PRIMARY error ONLY:**");
        prompt.AppendLine("1. Extract the EXACT error message (verbatim from log)");
        prompt.AppendLine("2. Identify the SPECIFIC file and line number");
        prompt.AppendLine("3. Extract code snippet if available (5-10 lines around error)");
        prompt.AppendLine("4. Extract stack trace if present");
        prompt.AppendLine("5. Determine expected vs actual behavior");
        prompt.AppendLine();

        prompt.AppendLine("## Response Format (JSON only, no markdown):");
        prompt.AppendLine("{");
        prompt.AppendLine("  \"errorType\": \"CompilationError|TestFailure|DependencyIssue|ConfigurationError|RuntimeError|NetworkError\",");
        prompt.AppendLine("  \"errorMessage\": \"EXACT error message from log\",");
        prompt.AppendLine("  \"fileName\": \"path/to/file.cs\",");
        prompt.AppendLine("  \"lineNumber\": 123,");
        prompt.AppendLine("  \"codeSnippet\": \"code around the error\",");
        prompt.AppendLine("  \"stackTrace\": [\"line1\", \"line2\"],");
        prompt.AppendLine("  \"expectedBehavior\": \"what should happen\",");
        prompt.AppendLine("  \"actualBehavior\": \"what actually happened\",");
        prompt.AppendLine("  \"affectedFiles\": [\"file1.cs\", \"file2.cs\"],");
        prompt.AppendLine("  \"extractionConfidence\": 0.95");
        prompt.AppendLine("}");
        prompt.AppendLine();

        prompt.AppendLine("CRITICAL REQUIREMENTS:");
        prompt.AppendLine("- errorMessage must be the EXACT text from the log (copy-paste), not a summary");
        prompt.AppendLine("- fileName must be a specific file path, not a module or directory");
        prompt.AppendLine("- lineNumber must be an integer (0 if not available)");
        prompt.AppendLine("- Be PRECISE. If you cannot find specific details, set extractionConfidence low");
        prompt.AppendLine("- affectedFiles should ONLY include files directly related to the PRIMARY error");
        prompt.AppendLine("- DO NOT include .csproj or configuration files in affectedFiles unless that's the PRIMARY error");
        prompt.AppendLine("- Return ONLY JSON, no additional text");
        prompt.AppendLine();
        prompt.AppendLine("**For Test Assertion Failures:**");
        prompt.AppendLine("- If you see 'Assert.Equal() Failure: Expected: X, Actual: Y':");
        prompt.AppendLine("  * errorType should be 'TestFailure'");
        prompt.AppendLine("  * expectedBehavior should be 'Expected: X'");
        prompt.AppendLine("  * actualBehavior should be 'Actual: Y'");
        prompt.AppendLine("  * fileName should be the TEST file (e.g., BasicTests.cs)");
        prompt.AppendLine("  * affectedFiles should be [\"TestFile.cs\"] ONLY (not implementation files)");

        return prompt.ToString();
    }

    private ErrorAnalysis ParseErrorAnalysis(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var analysis = JsonSerializer.Deserialize<ErrorAnalysis>(json, options);
                return analysis ?? CreateDefaultAnalysis("Deserialization returned null");
            }

            return CreateDefaultAnalysis("No valid JSON found in response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing error analysis response");
            return CreateDefaultAnalysis($"Parse error: {ex.Message}");
        }
    }

    private ErrorAnalysis CreateDefaultAnalysis(string reason)
    {
        return new ErrorAnalysis
        {
            ErrorType = "Unknown",
            ErrorMessage = reason,
            ExtractionConfidence = 0.0
        };
    }
}
