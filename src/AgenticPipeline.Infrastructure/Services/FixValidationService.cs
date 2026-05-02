using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Service for validating proposed fixes before committing
/// </summary>
public class FixValidationService
{
    private readonly IClaudeService _claudeService;
    private readonly ILogger<FixValidationService> _logger;

    public FixValidationService(
        IClaudeService claudeService,
        ILogger<FixValidationService> logger)
    {
        _claudeService = claudeService;
        _logger = logger;
    }

    /// <summary>
    /// Validates a proposed fix against the original error analysis
    /// </summary>
    public async Task<FixValidationResult> ValidateFixAsync(
        AutoFixResult proposedFix,
        ErrorAnalysis errorAnalysis,
        OrchestrationResult orchestrationResult,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Validating proposed fix for error: {ErrorType}", errorAnalysis.ErrorType);

            // Pre-validation: Check for unrelated file changes
            var preValidation = ValidateFileRelevance(proposedFix, errorAnalysis, orchestrationResult);
            if (!preValidation.IsValid)
            {
                return preValidation;
            }

            var validationPrompt = BuildValidationPrompt(proposedFix, errorAnalysis, orchestrationResult);

            var response = await _claudeService.SendMessageAsync(
                validationPrompt,
                cancellationToken: cancellationToken);

            var validation = ParseValidationResponse(response);

            _logger.LogInformation(
                "Validation complete: IsValid={IsValid}, Confidence={Confidence}, Recommendation={Recommendation}",
                validation.IsValid,
                validation.Confidence,
                validation.Recommendation);

            return validation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during fix validation");
            return new FixValidationResult
            {
                IsValid = false,
                Confidence = 0.0,
                Recommendation = "Reject",
                Reasoning = $"Validation error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Performs impact analysis to check if fix might break other code
    /// </summary>
    public async Task<ImpactAnalysisResult> AnalyzeImpactAsync(
        AutoFixResult proposedFix,
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Performing impact analysis for proposed fix");

            var impactPrompt = BuildImpactAnalysisPrompt(proposedFix);

            var response = await _claudeService.SendMessageAsync(
                impactPrompt,
                cancellationToken: cancellationToken);

            var impact = ParseImpactAnalysisResponse(response);

            _logger.LogInformation(
                "Impact analysis complete: RiskLevel={RiskLevel}, AffectedAreas={Count}",
                impact.RiskLevel,
                impact.AffectedAreas.Count);

            return impact;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during impact analysis");
            return new ImpactAnalysisResult
            {
                RiskLevel = "High",
                Reasoning = $"Impact analysis error: {ex.Message}",
                PotentialSideEffects = new List<string> { "Unable to determine impact due to analysis error" }
            };
        }
    }

    private string BuildValidationPrompt(
        AutoFixResult proposedFix,
        ErrorAnalysis errorAnalysis,
        OrchestrationResult orchestrationResult)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are a critical code reviewer validating an automatic fix proposal.");
        prompt.AppendLine("Be STRICT. Only approve fixes you are >90% confident will work correctly.");
        prompt.AppendLine();

        prompt.AppendLine("## Original Error Details");
        prompt.AppendLine($"Error Type: {errorAnalysis.ErrorType}");
        prompt.AppendLine($"Error Message: {errorAnalysis.ErrorMessage}");
        prompt.AppendLine($"File: {errorAnalysis.FileName}");
        prompt.AppendLine($"Line: {errorAnalysis.LineNumber}");
        prompt.AppendLine($"Expected Behavior: {errorAnalysis.ExpectedBehavior}");
        prompt.AppendLine($"Actual Behavior: {errorAnalysis.ActualBehavior}");
        prompt.AppendLine();

        if (!string.IsNullOrEmpty(errorAnalysis.CodeSnippet))
        {
            prompt.AppendLine("## Code Context");
            prompt.AppendLine("```");
            prompt.AppendLine(errorAnalysis.CodeSnippet);
            prompt.AppendLine("```");
            prompt.AppendLine();
        }

        prompt.AppendLine("## Proposed Fix");
        prompt.AppendLine($"Description: {proposedFix.FixDescription}");
        prompt.AppendLine($"Fix Confidence: {proposedFix.Confidence}");
        prompt.AppendLine();

        prompt.AppendLine("### File Changes:");
        foreach (var change in proposedFix.FileChanges)
        {
            prompt.AppendLine($"- File: {change.FilePath}");
            prompt.AppendLine($"  Change Type: {change.ChangeType}");
            prompt.AppendLine($"  Reason: {change.ChangeReason}");

            if (change.LineChanges != null && change.LineChanges.Any())
            {
                prompt.AppendLine("  Line Changes:");
                foreach (var lineChange in change.LineChanges)
                {
                    prompt.AppendLine($"    Line {lineChange.LineNumber} ({lineChange.ChangeType}):");
                    prompt.AppendLine($"      Original: {lineChange.OriginalLine}");
                    prompt.AppendLine($"      New:      {lineChange.NewLine}");
                }
            }
            prompt.AppendLine();
        }

        prompt.AppendLine("## Validation Questions");
        prompt.AppendLine("Analyze the proposed fix critically and answer:");
        prompt.AppendLine();
        prompt.AppendLine("1. Does this fix DIRECTLY address the specific error message above?");
        prompt.AppendLine("2. Will this fix solve the root cause, not just hide the symptom?");
        prompt.AppendLine("3. Could this change break any existing functionality?");
        prompt.AppendLine("4. Is the fix complete and minimal (no unnecessary changes)?");
        prompt.AppendLine("5. Are there any edge cases or potential side effects?");
        prompt.AppendLine();
        prompt.AppendLine("## SPECIAL CONSIDERATIONS FOR TEST FAILURES");
        prompt.AppendLine();
        prompt.AppendLine("If the error type is 'TestFailure' with a simple assertion mismatch:");
        prompt.AppendLine("- **Pattern**: Test expects X, but actual value is Y (e.g., 'Assert.Equal(21, count)' fails with actual=20)");
        prompt.AppendLine("- **Low Risk Scenario**: When the assertion is checking a COUNT or LITERAL VALUE and:");
        prompt.AppendLine("  * The implementation appears complete and functional");
        prompt.AppendLine("  * There's no evidence of missing data or broken logic");
        prompt.AppendLine("  * The fix ONLY changes the expected value in the test (no implementation changes)");
        prompt.AppendLine("  * The actual value is reasonable (not 0, not null, not error state)");
        prompt.AppendLine("- **In these cases**: It's SAFE to update the test expectation to match reality");
        prompt.AppendLine("- **Confidence should be 0.70-0.85** for simple assertion fixes (not 0.95, but not 0.30 either)");
        prompt.AppendLine();
        prompt.AppendLine("**When to be MORE critical**:");
        prompt.AppendLine("- If the fix adds/removes data from implementation (RED FLAG - likely wrong approach)");
        prompt.AppendLine("- If the fix changes business logic, not just test expectations");
        prompt.AppendLine("- If the actual value seems wrong (null, 0, empty, error message)");
        prompt.AppendLine("- If multiple unrelated files are being changed");
        prompt.AppendLine();
        prompt.AppendLine("**Example GOOD fix for test failure**:");
        prompt.AppendLine("```");
        prompt.AppendLine("Error: Assert.Equal(21, customers.Count) failed with actual=20");
        prompt.AppendLine("Fix: Change test line to Assert.Equal(20, customers.Count)");
        prompt.AppendLine("Confidence: 0.75 (reasonable - test expectation was likely wrong)");
        prompt.AppendLine("```");
        prompt.AppendLine();
        prompt.AppendLine("**Example BAD fix for test failure**:");
        prompt.AppendLine("```");
        prompt.AppendLine("Error: Assert.Equal(21, customers.Count) failed with actual=20");
        prompt.AppendLine("Fix: Add fake 21st customer to GenerateCustomers() method");
        prompt.AppendLine("Confidence: 0.10 (REJECT - adding fake data to pass test)");
        prompt.AppendLine("```");
        prompt.AppendLine();

        prompt.AppendLine("## Response Format");
        prompt.AppendLine("Respond with ONLY a JSON object (no markdown, no explanation):");
        prompt.AppendLine("```json");
        prompt.AppendLine("{");
        prompt.AppendLine("  \"isValid\": true,");
        prompt.AppendLine("  \"confidence\": 0.95,");
        prompt.AppendLine("  \"addressesRootCause\": true,");
        prompt.AppendLine("  \"couldBreakOtherCode\": false,");
        prompt.AppendLine("  \"isComplete\": true,");
        prompt.AppendLine("  \"isMinimal\": true,");
        prompt.AppendLine("  \"validationIssues\": [],");
        prompt.AppendLine("  \"recommendation\": \"Proceed\",");
        prompt.AppendLine("  \"reasoning\": \"Detailed explanation of your assessment\"");
        prompt.AppendLine("}");
        prompt.AppendLine("```");
        prompt.AppendLine();
        prompt.AppendLine("Recommendations: \"Proceed\" (approved), \"Refine\" (needs improvement), \"Reject\" (fundamentally wrong)");
        prompt.AppendLine();
        prompt.AppendLine("BE CRITICAL. Set isValid=false if you have ANY significant concerns.");

        return prompt.ToString();
    }

    private string BuildImpactAnalysisPrompt(AutoFixResult proposedFix)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are a software architect analyzing the potential impact of a code change.");
        prompt.AppendLine();

        prompt.AppendLine("## Proposed Changes");
        foreach (var change in proposedFix.FileChanges)
        {
            prompt.AppendLine($"File: {change.FilePath}");
            prompt.AppendLine($"Change: {change.ChangeReason}");
            prompt.AppendLine();
        }

        prompt.AppendLine("## Analysis Required");
        prompt.AppendLine("Assess the potential impact:");
        prompt.AppendLine("1. What other parts of the codebase might be affected?");
        prompt.AppendLine("2. Are there any potential side effects?");
        prompt.AppendLine("3. What is the risk level (Low, Medium, High)?");
        prompt.AppendLine("4. What areas should be tested after this change?");
        prompt.AppendLine();

        prompt.AppendLine("## Response Format (JSON only):");
        prompt.AppendLine("{");
        prompt.AppendLine("  \"riskLevel\": \"Low\",");
        prompt.AppendLine("  \"affectedAreas\": [\"list of affected areas\"],");
        prompt.AppendLine("  \"potentialSideEffects\": [\"list of potential issues\"],");
        prompt.AppendLine("  \"testingRecommendations\": [\"what to test\"],");
        prompt.AppendLine("  \"reasoning\": \"explanation\"");
        prompt.AppendLine("}");

        return prompt.ToString();
    }

    private FixValidationResult ParseValidationResponse(string response)
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

                var result = JsonSerializer.Deserialize<FixValidationResult>(json, options);
                return result ?? CreateFailedValidation("Deserialization returned null");
            }

            return CreateFailedValidation("No valid JSON found in response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing validation response");
            return CreateFailedValidation($"Parse error: {ex.Message}");
        }
    }

    private ImpactAnalysisResult ParseImpactAnalysisResponse(string response)
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

                var result = JsonSerializer.Deserialize<ImpactAnalysisResult>(json, options);
                return result ?? new ImpactAnalysisResult
                {
                    RiskLevel = "High",
                    Reasoning = "Failed to parse impact analysis"
                };
            }

            return new ImpactAnalysisResult
            {
                RiskLevel = "High",
                Reasoning = "No valid JSON in response"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing impact analysis response");
            return new ImpactAnalysisResult
            {
                RiskLevel = "High",
                Reasoning = $"Parse error: {ex.Message}"
            };
        }
    }

    private FixValidationResult CreateFailedValidation(string reason)
    {
        return new FixValidationResult
        {
            IsValid = false,
            Confidence = 0.0,
            Recommendation = "Reject",
            Reasoning = reason
        };
    }

    /// <summary>
    /// Pre-validation: Check if file changes are relevant to the error type
    /// </summary>
    private FixValidationResult ValidateFileRelevance(
        AutoFixResult proposedFix,
        ErrorAnalysis errorAnalysis,
        OrchestrationResult orchestrationResult)
    {
        // Get error file from analysis
        var errorFile = errorAnalysis.FileName;
        var errorType = orchestrationResult.Failure?.ErrorType ?? errorAnalysis.ErrorType;

        // Configuration file patterns
        var configFilePatterns = new[] { ".csproj", ".config", ".json", ".xml", ".yml", ".yaml" };
        var testFilePatterns = new[] { "test", "tests", "spec", "specs" };

        var changedFiles = proposedFix.FileChanges.Select(fc => fc.FilePath.ToLowerInvariant()).ToList();

        // Check 1: For TestFailure, should only modify test files (not config files)
        if (errorType == "TestFailure")
        {
            var configFilesChanged = changedFiles.Where(f => configFilePatterns.Any(pattern => f.EndsWith(pattern))).ToList();

            if (configFilesChanged.Any())
            {
                _logger.LogWarning(
                    "TestFailure fix attempting to modify configuration files: {ConfigFiles}",
                    string.Join(", ", configFilesChanged));

                return new FixValidationResult
                {
                    IsValid = false,
                    Confidence = 0.0,
                    Recommendation = "Reject",
                    Reasoning = $"Fix is modifying configuration files ({string.Join(", ", configFilesChanged)}) for a TestFailure. " +
                                "Test failures should only modify test files or implementation files, not configuration. " +
                                "This suggests the fix is addressing the wrong issue."
                };
            }
        }

        // Check 2: For CompilationError or DependencyIssue, config changes might be valid
        // But still check if BOTH test AND config files are being modified (suspicious)
        var hasTestFiles = changedFiles.Any(f => testFilePatterns.Any(pattern => f.Contains(pattern)));
        var hasConfigFiles = changedFiles.Any(f => configFilePatterns.Any(pattern => f.EndsWith(pattern)));

        if (hasTestFiles && hasConfigFiles && errorType == "TestFailure")
        {
            _logger.LogWarning(
                "Fix attempting to modify BOTH test files AND config files for a TestFailure. This is suspicious.");

            return new FixValidationResult
            {
                IsValid = false,
                Confidence = 0.0,
                Recommendation = "Reject",
                Reasoning = "Fix is modifying both test files and configuration files. " +
                            "This suggests multiple unrelated errors are being fixed in one commit. " +
                            "Fix should focus on a single error."
            };
        }

        // Check 3: If error is in specific file, check if that file is being modified
        if (!string.IsNullOrEmpty(errorFile))
        {
            var errorFileName = System.IO.Path.GetFileName(errorFile).ToLowerInvariant();
            var isErrorFileModified = changedFiles.Any(f => f.Contains(errorFileName) || errorFileName.Contains(f));

            if (!isErrorFileModified && changedFiles.Count > 0)
            {
                _logger.LogInformation(
                    "Fix is not modifying the error file ({ErrorFile}). Changed files: {ChangedFiles}",
                    errorFile,
                    string.Join(", ", changedFiles));

                // This is informational only - sometimes related files need to be changed
                // Don't reject, just log for awareness
            }
        }

        // Passed pre-validation
        return new FixValidationResult
        {
            IsValid = true,
            Confidence = 1.0,
            Recommendation = "Proceed",
            Reasoning = "File changes appear relevant to error type"
        };
    }
}

/// <summary>
/// Result of impact analysis
/// </summary>
public class ImpactAnalysisResult
{
    public string RiskLevel { get; set; } = "Unknown";
    public List<string> AffectedAreas { get; set; } = new();
    public List<string> PotentialSideEffects { get; set; } = new();
    public List<string> TestingRecommendations { get; set; } = new();
    public string Reasoning { get; set; } = string.Empty;
}
