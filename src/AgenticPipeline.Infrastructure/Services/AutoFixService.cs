using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using Microsoft.Extensions.Logging;
using Octokit;
using System.Text;
using System.Text.Json;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Service for generating and applying automatic fixes
/// </summary>
public class AutoFixService : IAutoFixService
{
    private readonly IClaudeService _claudeService;
    private readonly GitHubClient _githubClient;
    private readonly ILogger<AutoFixService> _logger;
    private readonly ErrorExtractionService _errorExtractionService;
    private readonly FixValidationService _fixValidationService;

    public AutoFixService(
        IClaudeService claudeService,
        GitHubClient githubClient,
        ILogger<AutoFixService> logger,
        ErrorExtractionService errorExtractionService,
        FixValidationService fixValidationService)
    {
        _claudeService = claudeService;
        _githubClient = githubClient;
        _logger = logger;
        _errorExtractionService = errorExtractionService;
        _fixValidationService = fixValidationService;
    }

    public async Task<AutoFixResult> GenerateFixWithRefinementAsync(
        OrchestrationResult orchestrationResult,
        string owner,
        string repo,
        AutoFixSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        settings ??= new AutoFixSettings();
        var attemptLogs = new List<FixAttemptLog>();

        try
        {
            _logger.LogInformation(
                "Starting fix generation with refinement for orchestration {OrchestrationId}",
                orchestrationResult.OrchestrationId);

            // Phase 1: Extract detailed error information
            _logger.LogInformation("Phase 1: Extracting detailed error information");
            var errorAnalysis = await _errorExtractionService.ExtractErrorDetailsAsync(
                orchestrationResult.Failure?.LogContent ?? string.Empty,
                orchestrationResult.Failure,
                cancellationToken);

            if (errorAnalysis.ExtractionConfidence < 0.6)
            {
                _logger.LogWarning("Error extraction confidence too low: {Confidence}", errorAnalysis.ExtractionConfidence);
                return new AutoFixResult
                {
                    FixGenerated = false,
                    IsSafeToApply = false,
                    Reason = $"Could not extract detailed error information (confidence: {errorAnalysis.ExtractionConfidence:P0})",
                    ErrorAnalysis = errorAnalysis
                };
            }

            _logger.LogInformation(
                "Error extracted: Type={ErrorType}, File={FileName}, Line={LineNumber}, Confidence={Confidence}",
                errorAnalysis.ErrorType,
                errorAnalysis.FileName,
                errorAnalysis.LineNumber,
                errorAnalysis.ExtractionConfidence);

            // Phase 2: Generate and refine fix with validation loop
            _logger.LogInformation("Phase 2: Generating fix with validation");
            AutoFixResult? bestFix = null;
            double bestConfidence = 0.0;

            for (int attempt = 1; attempt <= settings.MaxFixRefinementAttempts; attempt++)
            {
                _logger.LogInformation("Fix generation attempt {Attempt}/{MaxAttempts}", attempt, settings.MaxFixRefinementAttempts);

                var attemptLog = new FixAttemptLog
                {
                    AttemptNumber = attempt,
                    Timestamp = DateTime.UtcNow,
                    RcaConfidence = orchestrationResult.Failure?.ConfidenceScore ?? 0.0
                };

                // Generate fix
                var fixResult = await GenerateFixAsync(
                    orchestrationResult,
                    owner,
                    repo,
                    errorAnalysis,
                    cancellationToken);

                if (!fixResult.FixGenerated)
                {
                    attemptLog.Outcome = "FixNotGenerated";
                    attemptLog.Notes = fixResult.Reason ?? "Unknown reason";
                    attemptLogs.Add(attemptLog);
                    _logger.LogWarning("Fix not generated on attempt {Attempt}: {Reason}", attempt, fixResult.Reason);
                    continue;
                }

                attemptLog.FixConfidence = fixResult.Confidence;
                fixResult.ErrorAnalysis = errorAnalysis;

                // Phase 3: Validate the fix
                if (settings.EnableValidation)
                {
                    _logger.LogInformation("Phase 3: Validating generated fix");
                    var validation = await _fixValidationService.ValidateFixAsync(
                        fixResult,
                        errorAnalysis,
                        orchestrationResult,
                        cancellationToken);

                    fixResult.ValidationResult = validation;
                    fixResult.ValidationPassed = validation.IsValid;
                    attemptLog.ValidationConfidence = validation.Confidence;
                    attemptLog.ValidationPassed = validation.IsValid;
                    attemptLog.ValidationFeedback = validation.Reasoning;
                    attemptLog.ValidationIssues = validation.ValidationIssues;

                    if (!validation.IsValid && validation.Recommendation == "Refine" && attempt < settings.MaxFixRefinementAttempts)
                    {
                        attemptLog.Outcome = "Refined";
                        attemptLogs.Add(attemptLog);
                        _logger.LogInformation(
                            "Validation failed (recommendation: Refine). Issues: {Issues}",
                            string.Join(", ", validation.ValidationIssues));

                        // Add validation feedback to orchestration for next iteration
                        orchestrationResult.Failure!.DetailedAnalysis += $"\n\nPrevious fix attempt failed validation:\n{validation.Reasoning}\nIssues:\n- {string.Join("\n- ", validation.ValidationIssues)}";
                        continue;
                    }

                    if (!validation.IsValid && validation.Recommendation == "Reject")
                    {
                        attemptLog.Outcome = "Rejected";
                        attemptLogs.Add(attemptLog);
                        _logger.LogWarning("Validation rejected fix: {Reasoning}", validation.Reasoning);
                        continue;
                    }
                }

                // Calculate overall confidence
                fixResult.OverallConfidence = CalculateOverallConfidence(
                    orchestrationResult.Failure?.ConfidenceScore ?? 0.0,
                    errorAnalysis.ExtractionConfidence,
                    fixResult.Confidence,
                    fixResult.ValidationResult?.Confidence ?? 1.0);

                attemptLog.OverallConfidence = fixResult.OverallConfidence;

                _logger.LogInformation(
                    "Overall confidence: {Confidence} (RCA: {RcaConf}, Extraction: {ExtConf}, Fix: {FixConf}, Validation: {ValConf})",
                    fixResult.OverallConfidence,
                    attemptLog.RcaConfidence,
                    errorAnalysis.ExtractionConfidence,
                    fixResult.Confidence,
                    fixResult.ValidationResult?.Confidence ?? 1.0);

                // Check if this is the best fix so far
                if (fixResult.OverallConfidence > bestConfidence)
                {
                    bestFix = fixResult;
                    bestConfidence = fixResult.OverallConfidence;
                }

                // Determine confidence threshold based on error type
                // For TestFailure: lower threshold (0.65) since test assertion fixes are lower risk
                // For other errors: standard threshold (0.90)
                var errorType = orchestrationResult.Failure?.ErrorType ?? "Unknown";
                var confidenceThreshold = errorType == "TestFailure" ? 0.65 : settings.MinConfidenceForCommit;

                _logger.LogInformation(
                    "Using confidence threshold {Threshold} for error type {ErrorType}",
                    confidenceThreshold,
                    errorType);

                // If confidence meets threshold, commit this fix
                if (fixResult.OverallConfidence >= confidenceThreshold)
                {
                    attemptLog.Outcome = "Committed";
                    attemptLogs.Add(attemptLog);
                    fixResult.AttemptLogs = attemptLogs;

                    _logger.LogInformation(
                        "Fix approved with confidence {Confidence} >= {MinConfidence} (error type: {ErrorType})",
                        fixResult.OverallConfidence,
                        confidenceThreshold,
                        errorType);

                    return fixResult;
                }

                attemptLog.Outcome = "BelowThreshold";
                attemptLog.Notes = $"Overall confidence {fixResult.OverallConfidence:P0} below threshold {confidenceThreshold:P0} (error type: {errorType})";
                attemptLogs.Add(attemptLog);
            }

            // If we got here, no fix met the threshold
            if (bestFix != null)
            {
                var errorType = orchestrationResult.Failure?.ErrorType ?? "Unknown";
                var confidenceThreshold = errorType == "TestFailure" ? 0.65 : settings.MinConfidenceForCommit;

                _logger.LogWarning(
                    "No fix met confidence threshold. Best confidence: {BestConfidence} < {MinConfidence} (error type: {ErrorType})",
                    bestConfidence,
                    confidenceThreshold,
                    errorType);

                bestFix.IsSafeToApply = false;
                bestFix.Reason = $"Best fix confidence ({bestConfidence:P0}) below threshold ({confidenceThreshold:P0}) for {errorType}";
                bestFix.AttemptLogs = attemptLogs;
                return bestFix;
            }

            return new AutoFixResult
            {
                FixGenerated = false,
                IsSafeToApply = false,
                Reason = $"Failed to generate valid fix after {settings.MaxFixRefinementAttempts} attempts",
                ErrorAnalysis = errorAnalysis,
                AttemptLogs = attemptLogs
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during fix generation with refinement");
            return new AutoFixResult
            {
                FixGenerated = false,
                IsSafeToApply = false,
                Reason = $"Error during fix generation: {ex.Message}",
                AttemptLogs = attemptLogs
            };
        }
    }

    public async Task<AutoFixResult> GenerateFixAsync(
        OrchestrationResult orchestrationResult,
        string owner,
        string repo,
        ErrorAnalysis? errorAnalysis = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Attempting to generate auto-fix for orchestration {OrchestrationId}",
                orchestrationResult.OrchestrationId);

            // Check if failure analysis is available
            if (orchestrationResult.Failure == null)
            {
                return new AutoFixResult
                {
                    FixGenerated = false,
                    IsSafeToApply = false,
                    Reason = "No failure analysis available"
                };
            }

            // Check confidence threshold
            if (orchestrationResult.Failure.ConfidenceScore < 0.8)
            {
                return new AutoFixResult
                {
                    FixGenerated = false,
                    IsSafeToApply = false,
                    Reason = $"Confidence too low ({orchestrationResult.Failure.ConfidenceScore:P0}) for auto-fix"
                };
            }

            // Build context for fix generation
            var context = new StringBuilder();
            context.AppendLine($"Error Type: {orchestrationResult.Failure.ErrorType}");
            context.AppendLine($"Root Cause: {orchestrationResult.Failure.RootCause}");
            context.AppendLine($"Detailed Analysis: {orchestrationResult.Failure.DetailedAnalysis}");
            context.AppendLine();
            context.AppendLine("Recommendations:");
            var recommendations = JsonSerializer.Deserialize<List<string>>(
                orchestrationResult.Failure.RecommendationsJson) ?? new List<string>();
            foreach (var rec in recommendations)
            {
                context.AppendLine($"- {rec}");
            }
            context.AppendLine();
            context.AppendLine("Affected Components:");
            var components = JsonSerializer.Deserialize<List<string>>(
                orchestrationResult.Failure.AffectedComponentsJson) ?? new List<string>();
            foreach (var comp in components)
            {
                context.AppendLine($"- {comp}");
            }

            // Fetch file contents for affected components
            var fileContents = new Dictionary<string, string>();
            // Fetch all affected components (no limit) - Claude can handle large context
            // Filter to only valid file paths (not build step names or generic terms)
            var validComponents = components.Where(IsValidFilePath).ToList();

            if (validComponents.Count < components.Count)
            {
                var invalidComponents = components.Except(validComponents).ToList();
                _logger.LogWarning(
                    "Filtered out {Count} invalid components (not file paths): {InvalidComponents}",
                    invalidComponents.Count,
                    string.Join(", ", invalidComponents));
            }

            foreach (var component in validComponents)
            {
                try
                {
                    var fileContent = await _githubClient.Repository.Content.GetAllContents(owner, repo, component);
                    if (fileContent.Count > 0)
                    {
                        fileContents[component] = fileContent[0].Content;
                        _logger.LogInformation("Fetched file content for {Component} ({Length} chars)", component, fileContent[0].Content.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch file content for {Component}", component);
                }
            }

            // Generate fix using Claude
            var prompt = BuildFixGenerationPrompt(context.ToString(), fileContents, orchestrationResult, errorAnalysis);

            var response = await _claudeService.SendMessageAsync(prompt, cancellationToken);

            // Parse the response (expecting JSON)
            var fixResult = ParseFixResponse(response);

            if (fixResult.FixGenerated)
            {
                // Generate PR metadata
                fixResult.BranchName = $"autofix/{orchestrationResult.Failure.ErrorType.ToLower()}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                fixResult.PrTitle = $"[Auto-Fix] {orchestrationResult.Failure.ErrorType}: {orchestrationResult.Failure.RootCause.Substring(0, Math.Min(80, orchestrationResult.Failure.RootCause.Length))}";
                fixResult.PrBody = BuildPRBody(orchestrationResult, fixResult);
            }

            _logger.LogInformation(
                "Auto-fix generation completed: FixGenerated={FixGenerated}, IsSafeToApply={IsSafeToApply}",
                fixResult.FixGenerated,
                fixResult.IsSafeToApply);

            return fixResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating auto-fix");
            return new AutoFixResult
            {
                FixGenerated = false,
                IsSafeToApply = false,
                Reason = $"Error generating fix: {ex.Message}"
            };
        }
    }

    private double CalculateOverallConfidence(
        double rcaConfidence,
        double extractionConfidence,
        double fixConfidence,
        double validationConfidence)
    {
        // Weighted average with emphasis on validation and RCA
        // Weights: RCA=0.25, Extraction=0.20, Fix=0.25, Validation=0.30
        var overall = (rcaConfidence * 0.25) +
                     (extractionConfidence * 0.20) +
                     (fixConfidence * 0.25) +
                     (validationConfidence * 0.30);

        // Apply penalty if any component is very low
        if (rcaConfidence < 0.7 || extractionConfidence < 0.6 || fixConfidence < 0.7 || validationConfidence < 0.7)
        {
            overall *= 0.85; // 15% penalty
        }

        return Math.Max(0.0, Math.Min(1.0, overall));
    }

    public async Task<int?> ApplyFixAndCreatePRAsync(
        AutoFixResult autoFixResult,
        string owner,
        string repo,
        string? baseBranch = null,
        double minConfidence = 0.9,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Applying auto-fix and creating PR in {Owner}/{Repo}",
                owner,
                repo);

            // Check confidence threshold
            if (autoFixResult.OverallConfidence > 0 && autoFixResult.OverallConfidence < minConfidence)
            {
                _logger.LogWarning(
                    "Fix confidence {Confidence} below minimum {MinConfidence}. Aborting PR creation.",
                    autoFixResult.OverallConfidence,
                    minConfidence);
                return null;
            }

            // Check validation if it was performed
            if (autoFixResult.ValidationResult != null && !autoFixResult.ValidationPassed)
            {
                _logger.LogWarning(
                    "Fix failed validation: {Reasoning}",
                    autoFixResult.ValidationResult.Reasoning);
                return null;
            }

            // Get the repository
            var repository = await _githubClient.Repository.Get(owner, repo);

            // Determine base branch
            if (string.IsNullOrEmpty(baseBranch))
            {
                baseBranch = repository.DefaultBranch;
            }

            // Get base branch reference
            var baseRef = await _githubClient.Git.Reference.Get(owner, repo, $"heads/{baseBranch}");
            var baseSha = baseRef.Object.Sha;

            // Create new branch
            var newBranch = await _githubClient.Git.Reference.Create(owner, repo, new NewReference(
                $"refs/heads/{autoFixResult.BranchName}",
                baseSha));

            _logger.LogInformation("Created branch {BranchName}", autoFixResult.BranchName);

            // Apply file changes
            foreach (var fileChange in autoFixResult.FileChanges)
            {
                try
                {
                    if (fileChange.ChangeType == FileChangeType.Modify || fileChange.ChangeType == FileChangeType.Create)
                    {
                        // Check if file exists
                        IReadOnlyList<RepositoryContent>? existingFile = null;
                        try
                        {
                            existingFile = await _githubClient.Repository.Content.GetAllContents(owner, repo, fileChange.FilePath);
                        }
                        catch (NotFoundException)
                        {
                            // File doesn't exist, that's okay if we're creating it
                        }

                        // Determine final content
                        string finalContent;
                        if (fileChange.LineChanges != null && fileChange.LineChanges.Count > 0)
                        {
                            // Apply line-based changes
                            if (existingFile == null || existingFile.Count == 0)
                            {
                                _logger.LogError("Cannot apply line changes to non-existent file {FilePath}", fileChange.FilePath);
                                continue;
                            }

                            finalContent = ApplyLineChanges(existingFile[0].Content, fileChange.LineChanges);
                            _logger.LogInformation("Applied {Count} line changes to {FilePath}", fileChange.LineChanges.Count, fileChange.FilePath);
                        }
                        else
                        {
                            // Use full content
                            finalContent = fileChange.NewContent ?? string.Empty;
                        }

                        if (existingFile != null && existingFile.Count > 0)
                        {
                            // Update existing file
                            await _githubClient.Repository.Content.UpdateFile(
                                owner,
                                repo,
                                fileChange.FilePath,
                                new UpdateFileRequest(
                                    $"Auto-fix: {fileChange.ChangeReason}",
                                    finalContent,
                                    existingFile[0].Sha,
                                    autoFixResult.BranchName));

                            _logger.LogInformation("Updated file {FilePath}", fileChange.FilePath);
                        }
                        else
                        {
                            // Create new file
                            await _githubClient.Repository.Content.CreateFile(
                                owner,
                                repo,
                                fileChange.FilePath,
                                new CreateFileRequest(
                                    $"Auto-fix: {fileChange.ChangeReason}",
                                    finalContent,
                                    autoFixResult.BranchName));

                            _logger.LogInformation("Created file {FilePath}", fileChange.FilePath);
                        }
                    }
                    else if (fileChange.ChangeType == FileChangeType.Delete)
                    {
                        var existingFile = await _githubClient.Repository.Content.GetAllContents(owner, repo, fileChange.FilePath);
                        if (existingFile.Count > 0)
                        {
                            await _githubClient.Repository.Content.DeleteFile(
                                owner,
                                repo,
                                fileChange.FilePath,
                                new DeleteFileRequest(
                                    $"Auto-fix: {fileChange.ChangeReason}",
                                    existingFile[0].Sha,
                                    autoFixResult.BranchName));

                            _logger.LogInformation("Deleted file {FilePath}", fileChange.FilePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying change to {FilePath}", fileChange.FilePath);
                }
            }

            // Create pull request
            var pr = await _githubClient.PullRequest.Create(owner, repo, new NewPullRequest(
                autoFixResult.PrTitle ?? "Auto-fix",
                autoFixResult.BranchName!,
                baseBranch)
            {
                Body = autoFixResult.PrBody
            });

            _logger.LogInformation("Created PR #{PrNumber}: {PrTitle}", pr.Number, pr.Title);

            return pr.Number;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying auto-fix and creating PR");
            return null;
        }
    }

    private string BuildFixGenerationPrompt(string context, Dictionary<string, string> fileContents, OrchestrationResult orchestrationResult, ErrorAnalysis? errorAnalysis)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are an expert software engineer tasked with generating an automatic fix for a CI/CD pipeline failure.");
        prompt.AppendLine();
        prompt.AppendLine("## Failure Analysis");
        prompt.AppendLine(context);
        prompt.AppendLine();

        if (errorAnalysis != null)
        {
            prompt.AppendLine("## Detailed Error Information");
            prompt.AppendLine($"Error Type: {errorAnalysis.ErrorType}");
            prompt.AppendLine($"Error Message: {errorAnalysis.ErrorMessage}");
            prompt.AppendLine($"File: {errorAnalysis.FileName}");
            prompt.AppendLine($"Line: {errorAnalysis.LineNumber}");
            if (!string.IsNullOrEmpty(errorAnalysis.ExpectedBehavior))
            {
                prompt.AppendLine($"Expected: {errorAnalysis.ExpectedBehavior}");
            }
            if (!string.IsNullOrEmpty(errorAnalysis.ActualBehavior))
            {
                prompt.AppendLine($"Actual: {errorAnalysis.ActualBehavior}");
            }
            if (!string.IsNullOrEmpty(errorAnalysis.CodeSnippet))
            {
                prompt.AppendLine("Code Context:");
                prompt.AppendLine("```");
                prompt.AppendLine(errorAnalysis.CodeSnippet);
                prompt.AppendLine("```");
            }
            if (errorAnalysis.StackTrace != null && errorAnalysis.StackTrace.Any())
            {
                prompt.AppendLine("Stack Trace:");
                // Include full stack trace for better error diagnosis
                foreach (var stackLine in errorAnalysis.StackTrace)
                {
                    prompt.AppendLine($"  {stackLine}");
                }
            }
            prompt.AppendLine();
        }

        if (fileContents.Count > 0)
        {
            prompt.AppendLine("## Current File Contents");
            foreach (var (filePath, content) in fileContents)
            {
                prompt.AppendLine($"### {filePath}");
                prompt.AppendLine("```");
                prompt.AppendLine(content);
                prompt.AppendLine("```");
                prompt.AppendLine();
            }
        }

        prompt.AppendLine("## Task");
        prompt.AppendLine("Analyze the failure and determine if an automatic fix can be safely generated.");
        prompt.AppendLine();
        prompt.AppendLine("**IMPORTANT SAFETY RULES:**");
        prompt.AppendLine("- Only generate fixes for simple, well-understood issues (syntax errors, missing imports, typos)");
        prompt.AppendLine("- DO NOT fix complex logic errors, architectural issues, or design problems");
        prompt.AppendLine("- DO NOT modify database schemas, configuration files, or security-sensitive code");
        prompt.AppendLine("- Set isSafeToApply=false if the fix requires human judgment or has potential side effects");
        prompt.AppendLine();
        prompt.AppendLine("**CRITICAL RULES FOR TEST FAILURES:**");
        prompt.AppendLine("- When error type is 'TestFailure' or you see assertion errors (Assert.Equal, Assert.True, etc.):");
        prompt.AppendLine("  * FIRST determine if the TEST expectation is wrong OR the IMPLEMENTATION is broken");
        prompt.AppendLine("  * If test asserts 'Expected: X, Actual: Y' and Y seems reasonable → FIX THE TEST to expect Y");
        prompt.AppendLine("  * DO NOT add fake/dummy data to make tests pass (e.g., don't add rows just to match a count)");
        prompt.AppendLine("  * DO NOT modify implementation data just to satisfy test expectations");
        prompt.AppendLine("  * ONLY fix implementation if there's clear broken logic (null reference, wrong calculation, etc.)");
        prompt.AppendLine("  * Example WRONG: Test expects 21 items, code has 20 → Adding fake 21st item ❌");
        prompt.AppendLine("  * Example RIGHT: Test expects 21 items, code has 20 → Change test to expect 20 ✅");
        prompt.AppendLine();
        prompt.AppendLine("**SINGLE ERROR FOCUS:**");
        prompt.AppendLine("- Fix ONLY the primary error that caused the build/test to fail");
        prompt.AppendLine("- DO NOT attempt to fix multiple unrelated errors in one commit");
        prompt.AppendLine("- DO NOT touch configuration files (.csproj, .config, .json) unless the PRIMARY error is a configuration issue");
        prompt.AppendLine("- If you see both a test failure AND a configuration error, FIX ONLY THE PRIMARY ONE");
        prompt.AppendLine("- Example WRONG: Test fails + .csproj has warning → Fix both ❌");
        prompt.AppendLine("- Example RIGHT: Test fails + .csproj has warning → Fix test only ✅");
        prompt.AppendLine();
        prompt.AppendLine("## Response Format");
        prompt.AppendLine("Respond with a JSON object in this exact format:");
        prompt.AppendLine("**IMPORTANT:** For efficiency, use 'lineChanges' instead of 'newContent' when possible.");
        prompt.AppendLine("Only use 'newContent' for very small files (< 50 lines) or when creating new files.");
        prompt.AppendLine();
        prompt.AppendLine("```json");
        prompt.AppendLine("{");
        prompt.AppendLine("  \"fixGenerated\": true,");
        prompt.AppendLine("  \"isSafeToApply\": true,");
        prompt.AppendLine("  \"confidence\": 0.95,");
        prompt.AppendLine("  \"fixDescription\": \"Brief description of the fix\",");
        prompt.AppendLine("  \"reason\": \"Explanation if not safe to apply\",");
        prompt.AppendLine("  \"fileChanges\": [");
        prompt.AppendLine("    {");
        prompt.AppendLine("      \"filePath\": \"path/to/file.cs\",");
        prompt.AppendLine("      \"changeType\": \"Modify\",");
        prompt.AppendLine("      \"lineChanges\": [");
        prompt.AppendLine("        {");
        prompt.AppendLine("          \"lineNumber\": 23,");
        prompt.AppendLine("          \"originalLine\": \"Assert.Equal(20, customers.Count());\",");
        prompt.AppendLine("          \"newLine\": \"Assert.Equal(21, customers.Count());\",");
        prompt.AppendLine("          \"changeType\": \"replace\"");
        prompt.AppendLine("        }");
        prompt.AppendLine("      ],");
        prompt.AppendLine("      \"changeReason\": \"Fixed off-by-one error in expected count\"");
        prompt.AppendLine("    }");
        prompt.AppendLine("  ]");
        prompt.AppendLine("}");
        prompt.AppendLine("```");
        prompt.AppendLine();
        prompt.AppendLine("**Note:** changeType can be 'replace', 'insert', or 'delete'. Use 'insert' to add a new line before lineNumber.");

        return prompt.ToString();
    }

    private AutoFixResult ParseFixResponse(string response)
    {
        try
        {
            // Log response length for diagnostics
            _logger.LogDebug("Parsing fix response: {Length} characters", response.Length);

            // Extract JSON from response (may be wrapped in markdown code blocks)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart);

                // Check if JSON looks truncated (common signs)
                if (!json.TrimEnd().EndsWith("}") && !json.TrimEnd().EndsWith("]"))
                {
                    _logger.LogWarning("JSON appears truncated (doesn't end with }} or ]). Length: {Length}", json.Length);
                    return new AutoFixResult
                    {
                        FixGenerated = false,
                        IsSafeToApply = false,
                        Reason = "Response was truncated - likely hit token limit. Try using lineChanges instead of full file content."
                    };
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false) }
                };

                var result = JsonSerializer.Deserialize<AutoFixResult>(json, options);

                if (result == null)
                {
                    _logger.LogWarning("Deserialization returned null");
                    return new AutoFixResult
                    {
                        FixGenerated = false,
                        IsSafeToApply = false,
                        Reason = "Failed to parse fix response"
                    };
                }

                _logger.LogInformation("Successfully parsed fix response: FixGenerated={FixGenerated}, FileChanges={FileChangeCount}",
                    result.FixGenerated, result.FileChanges.Count);

                return result;
            }

            _logger.LogWarning("No valid JSON found in response");
            return new AutoFixResult
            {
                FixGenerated = false,
                IsSafeToApply = false,
                Reason = "No valid JSON found in response"
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error. Response length: {Length} chars", response.Length);

            // Check if it's a truncation error
            if (ex.Message.Contains("Expected end of string") || ex.Message.Contains("reached end of data"))
            {
                return new AutoFixResult
                {
                    FixGenerated = false,
                    IsSafeToApply = false,
                    Reason = "Response was truncated due to token limit. Consider increasing MaxTokens or using smaller line-based changes."
                };
            }

            return new AutoFixResult
            {
                FixGenerated = false,
                IsSafeToApply = false,
                Reason = $"Error parsing response: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing fix response");
            return new AutoFixResult
            {
                FixGenerated = false,
                IsSafeToApply = false,
                Reason = $"Error parsing response: {ex.Message}"
            };
        }
    }

    private string ApplyLineChanges(string originalContent, List<LineChange> lineChanges)
    {
        // Detect original line ending style to preserve it
        string lineEnding = "\n"; // Default to LF (most common in Git)
        if (originalContent.Contains("\r\n"))
        {
            lineEnding = "\r\n"; // CRLF (Windows)
        }
        else if (originalContent.Contains("\r"))
        {
            lineEnding = "\r"; // CR (old Mac)
        }

        _logger.LogDebug("Detected line ending: {LineEnding}",
            lineEnding == "\r\n" ? "CRLF" : lineEnding == "\r" ? "CR" : "LF");

        var lines = originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
        var totalLines = lines.Count;

        _logger.LogInformation(
            "Applying {ChangeCount} line changes to file with {LineCount} lines",
            lineChanges.Count,
            totalLines);

        // Validate all line numbers first
        var validatedChanges = new List<LineChange>();
        foreach (var change in lineChanges)
        {
            var lineIndex = change.LineNumber - 1; // Convert to 0-based index

            // Validate line number
            if (lineIndex < 0 || lineIndex >= totalLines)
            {
                _logger.LogWarning(
                    "Line number {LineNumber} out of range (file has {LineCount} lines). Attempting to clamp to valid range.",
                    change.LineNumber,
                    totalLines);

                // Clamp to valid range
                if (lineIndex < 0)
                {
                    change.LineNumber = 1;
                    _logger.LogInformation("Clamped line number from {Original} to 1", lineIndex + 1);
                }
                else if (lineIndex >= totalLines)
                {
                    change.LineNumber = totalLines;
                    _logger.LogInformation("Clamped line number from {Original} to {Max}", lineIndex + 1, totalLines);
                }
            }

            // Validate original line matches (if provided)
            if (!string.IsNullOrEmpty(change.OriginalLine))
            {
                var actualLineIndex = change.LineNumber - 1;
                if (actualLineIndex >= 0 && actualLineIndex < lines.Count)
                {
                    var actualLine = lines[actualLineIndex].Trim();
                    var expectedLine = change.OriginalLine.Trim();

                    if (actualLine != expectedLine)
                    {
                        _logger.LogWarning(
                            "Line {LineNumber} content mismatch. Expected: '{Expected}', Actual: '{Actual}'. Applying change anyway.",
                            change.LineNumber,
                            expectedLine,
                            actualLine);
                    }
                }
            }

            validatedChanges.Add(change);
        }

        // Sort changes by line number in descending order to avoid index shifts
        var sortedChanges = validatedChanges.OrderByDescending(c => c.LineNumber).ToList();

        foreach (var change in sortedChanges)
        {
            var lineIndex = change.LineNumber - 1; // Convert to 0-based index

            // Double-check after sorting
            if (lineIndex < 0 || lineIndex >= lines.Count)
            {
                _logger.LogWarning(
                    "Skipping line {LineNumber} - still out of range after validation",
                    change.LineNumber);
                continue;
            }

            switch (change.ChangeType.ToLower())
            {
                case "replace":
                    if (change.NewLine != null)
                    {
                        lines[lineIndex] = change.NewLine;
                        _logger.LogDebug("Replaced line {LineNumber}: {OldLine} -> {NewLine}",
                            change.LineNumber, change.OriginalLine, change.NewLine);
                    }
                    break;

                case "insert":
                    if (change.NewLine != null)
                    {
                        lines.Insert(lineIndex, change.NewLine);
                        _logger.LogDebug("Inserted line {LineNumber}: {NewLine}",
                            change.LineNumber, change.NewLine);
                    }
                    break;

                case "delete":
                    lines.RemoveAt(lineIndex);
                    _logger.LogDebug("Deleted line {LineNumber}: {OldLine}",
                        change.LineNumber, change.OriginalLine);
                    break;

                default:
                    _logger.LogWarning("Unknown change type: {ChangeType}", change.ChangeType);
                    break;
            }
        }

        _logger.LogInformation("Successfully applied {Count} line changes", validatedChanges.Count);

        // Rejoin with the same line ending style as the original file
        return string.Join(lineEnding, lines);
    }

    private string BuildPRBody(OrchestrationResult orchestrationResult, AutoFixResult fixResult)
    {
        var body = new StringBuilder();
        body.AppendLine("## Automatic Fix Generated");
        body.AppendLine();
        body.AppendLine($"**Fix Confidence:** {fixResult.Confidence:P0}");
        if (fixResult.OverallConfidence > 0)
        {
            body.AppendLine($"**Overall Confidence:** {fixResult.OverallConfidence:P0}");
        }
        body.AppendLine($"**Fix Description:** {fixResult.FixDescription}");
        body.AppendLine();

        if (fixResult.ValidationResult != null)
        {
            body.AppendLine("## Validation Results");
            body.AppendLine($"**Status:** {(fixResult.ValidationPassed ? "PASSED" : "FAILED")}");
            body.AppendLine($"**Validation Confidence:** {fixResult.ValidationResult.Confidence:P0}");
            body.AppendLine($"**Addresses Root Cause:** {(fixResult.ValidationResult.AddressesRootCause ? "Yes" : "No")}");
            body.AppendLine($"**Could Break Other Code:** {(fixResult.ValidationResult.CouldBreakOtherCode ? "Yes" : "No")}");
            body.AppendLine($"**Recommendation:** {fixResult.ValidationResult.Recommendation}");
            if (!string.IsNullOrEmpty(fixResult.ValidationResult.Reasoning))
            {
                body.AppendLine($"**Reasoning:** {fixResult.ValidationResult.Reasoning}");
            }
            if (fixResult.ValidationResult.ValidationIssues.Any())
            {
                body.AppendLine("**Issues Found:**");
                foreach (var issue in fixResult.ValidationResult.ValidationIssues)
                {
                    body.AppendLine($"- {issue}");
                }
            }
            body.AppendLine();
        }

        if (fixResult.AttemptLogs.Any())
        {
            body.AppendLine($"## Fix Generation Attempts: {fixResult.AttemptLogs.Count}");
            foreach (var log in fixResult.AttemptLogs)
            {
                body.AppendLine($"**Attempt {log.AttemptNumber}** - {log.Outcome} (Overall: {log.OverallConfidence:P0})");
            }
            body.AppendLine();
        }

        if (fixResult.ErrorAnalysis != null)
        {
            body.AppendLine("## Detailed Error Analysis");
            body.AppendLine($"**Error Type:** {fixResult.ErrorAnalysis.ErrorType}");
            body.AppendLine($"**File:** {fixResult.ErrorAnalysis.FileName}:{fixResult.ErrorAnalysis.LineNumber}");
            body.AppendLine($"**Error Message:** {fixResult.ErrorAnalysis.ErrorMessage}");
            if (!string.IsNullOrEmpty(fixResult.ErrorAnalysis.ExpectedBehavior))
            {
                body.AppendLine($"**Expected:** {fixResult.ErrorAnalysis.ExpectedBehavior}");
            }
            if (!string.IsNullOrEmpty(fixResult.ErrorAnalysis.ActualBehavior))
            {
                body.AppendLine($"**Actual:** {fixResult.ErrorAnalysis.ActualBehavior}");
            }
            body.AppendLine();
        }
        body.AppendLine("## Original Failure Analysis");
        body.AppendLine();
        body.AppendLine($"**Error Type:** {orchestrationResult.Failure?.ErrorType}");
        body.AppendLine($"**Root Cause:** {orchestrationResult.Failure?.RootCause}");
        body.AppendLine();
        body.AppendLine("### Recommendations");
        var recommendations = JsonSerializer.Deserialize<List<string>>(
            orchestrationResult.Failure?.RecommendationsJson ?? "[]") ?? new List<string>();
        foreach (var rec in recommendations.Take(5))
        {
            body.AppendLine($"- {rec}");
        }
        body.AppendLine();
        body.AppendLine("## Changes Made");
        foreach (var change in fixResult.FileChanges)
        {
            body.AppendLine($"- **{change.FilePath}** ({change.ChangeType}): {change.ChangeReason}");

            // Show line changes if available
            if (change.LineChanges != null && change.LineChanges.Count > 0)
            {
                foreach (var lineChange in change.LineChanges.Take(10))
                {
                    body.AppendLine($"  - Line {lineChange.LineNumber}: `{lineChange.OriginalLine}` → `{lineChange.NewLine}`");
                }
                if (change.LineChanges.Count > 10)
                {
                    body.AppendLine($"  - _(and {change.LineChanges.Count - 10} more changes)_");
                }
            }
        }
        body.AppendLine();
        body.AppendLine("---");
        body.AppendLine($"*This PR was automatically generated by Agentic Pipeline (Orchestration ID: `{orchestrationResult.OrchestrationId}`)*");
        body.AppendLine();
        body.AppendLine("**⚠️ Please review carefully before merging!** This fix was generated automatically and may require adjustments.");

        return body.ToString();
    }

    public async Task<string?> CommitToExistingBranchAsync(
        AutoFixResult autoFixResult,
        string owner,
        string repo,
        string existingBranch,
        int attemptNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Committing additional fix atomically to existing branch {BranchName}, attempt {Attempt}",
                existingBranch,
                attemptNumber);

            // Get existing branch reference and commit
            var branchRef = await _githubClient.Git.Reference.Get(owner, repo, $"heads/{existingBranch}");
            var branchCommit = await _githubClient.Git.Commit.Get(owner, repo, branchRef.Object.Sha);
            var baseTreeSha = branchCommit.Tree.Sha;

            _logger.LogInformation(
                "Existing branch {Branch}: commit={CommitSha}, tree={TreeSha}",
                existingBranch,
                branchRef.Object.Sha,
                baseTreeSha);

            // Create new tree with all file changes
            var newTree = new NewTree { BaseTree = baseTreeSha };
            var filesProcessed = 0;

            foreach (var fileChange in autoFixResult.FileChanges)
            {
                try
                {
                    if (fileChange.ChangeType == FileChangeType.Modify || fileChange.ChangeType == FileChangeType.Create)
                    {
                        // Determine final content
                        string finalContent;

                        // Check if file exists on branch for line changes
                        if (fileChange.LineChanges != null && fileChange.LineChanges.Count > 0)
                        {
                            IReadOnlyList<RepositoryContent>? existingFile = null;
                            try
                            {
                                existingFile = await _githubClient.Repository.Content.GetAllContentsByRef(
                                    owner,
                                    repo,
                                    fileChange.FilePath,
                                    existingBranch);
                            }
                            catch (NotFoundException)
                            {
                                _logger.LogError("Cannot apply line changes to non-existent file {FilePath}", fileChange.FilePath);
                                continue;
                            }

                            if (existingFile == null || existingFile.Count == 0)
                            {
                                _logger.LogError("Cannot apply line changes to non-existent file {FilePath}", fileChange.FilePath);
                                continue;
                            }

                            finalContent = ApplyLineChanges(existingFile[0].Content, fileChange.LineChanges);
                            _logger.LogInformation("Applied {Count} line changes to {FilePath}", fileChange.LineChanges.Count, fileChange.FilePath);
                        }
                        else
                        {
                            finalContent = fileChange.NewContent ?? string.Empty;
                        }

                        // Create blob for the file content
                        var blob = await _githubClient.Git.Blob.Create(owner, repo, new NewBlob
                        {
                            Content = finalContent,
                            Encoding = EncodingType.Utf8
                        });

                        _logger.LogInformation("Created blob {BlobSha} for {FilePath}", blob.Sha, fileChange.FilePath);

                        // Add blob to tree
                        newTree.Tree.Add(new NewTreeItem
                        {
                            Path = fileChange.FilePath,
                            Mode = "100644", // Regular file
                            Type = TreeType.Blob,
                            Sha = blob.Sha
                        });

                        filesProcessed++;
                    }
                    else if (fileChange.ChangeType == FileChangeType.Delete)
                    {
                        // Add deletion to tree (null SHA means delete)
                        newTree.Tree.Add(new NewTreeItem
                        {
                            Path = fileChange.FilePath,
                            Mode = "100644",
                            Type = TreeType.Blob,
                            Sha = null
                        });

                        filesProcessed++;
                        _logger.LogInformation("Marked {FilePath} for deletion", fileChange.FilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error preparing {FilePath} for commit", fileChange.FilePath);
                }
            }

            if (filesProcessed == 0)
            {
                _logger.LogWarning("No files were processed for commit");
                return null;
            }

            _logger.LogInformation("Creating Git tree with {FileCount} file change(s)", filesProcessed);

            // Create the tree
            var tree = await _githubClient.Git.Tree.Create(owner, repo, newTree);

            _logger.LogInformation("Created Git tree {TreeSha} with {FileCount} file(s)", tree.Sha, filesProcessed);

            // Build commit message
            var commitMessage = new StringBuilder();
            commitMessage.AppendLine($"Auto-fix attempt {attemptNumber}: {autoFixResult.FixDescription}");
            commitMessage.AppendLine();
            commitMessage.AppendLine($"Files modified: {filesProcessed}");
            foreach (var fileChange in autoFixResult.FileChanges)
            {
                commitMessage.AppendLine($"  - {fileChange.ChangeType}: {fileChange.FilePath}");
            }

            // Create single commit with all changes
            var commit = await _githubClient.Git.Commit.Create(owner, repo, new NewCommit(
                message: commitMessage.ToString(),
                tree: tree.Sha,
                parents: new[] { branchRef.Object.Sha }
            ));

            _logger.LogInformation(
                "Created atomic commit {CommitSha} with {FileCount} file(s)",
                commit.Sha,
                filesProcessed);

            // Update branch reference to point to new commit
            await _githubClient.Git.Reference.Update(owner, repo, $"heads/{existingBranch}", new ReferenceUpdate(commit.Sha));

            _logger.LogInformation(
                "Successfully committed changes atomically to existing branch {Branch}, commit SHA: {CommitSha}",
                existingBranch,
                commit.Sha);

            return commit.Sha;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error committing atomically to existing branch {Branch}", existingBranch);
            return null;
        }
    }

    public async Task<int?> CreatePRForBranchAsync(
        string branchName,
        string owner,
        string repo,
        string title,
        string body,
        string? baseBranch = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Creating PR for branch {BranchName} in {Owner}/{Repo}",
                branchName,
                owner,
                repo);

            // Get the repository
            var repository = await _githubClient.Repository.Get(owner, repo);

            // Determine base branch
            if (string.IsNullOrEmpty(baseBranch))
            {
                baseBranch = repository.DefaultBranch;
            }

            // Create pull request
            var pr = await _githubClient.PullRequest.Create(owner, repo, new NewPullRequest(
                title,
                branchName,
                baseBranch)
            {
                Body = body
            });

            _logger.LogInformation("Created PR #{PrNumber}: {PrTitle}", pr.Number, pr.Title);

            return pr.Number;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating PR for branch {BranchName}", branchName);
            return null;
        }
    }

    public async Task<string?> CreateBranchAndCommitAsync(
        AutoFixResult autoFixResult,
        string owner,
        string repo,
        string branchName,
        string? baseBranch = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Creating branch {BranchName} and committing ALL fixes atomically (NO PR) in {Owner}/{Repo}",
                branchName,
                owner,
                repo);

            // Get the repository
            var repository = await _githubClient.Repository.Get(owner, repo);

            // Determine base branch
            if (string.IsNullOrEmpty(baseBranch))
            {
                baseBranch = repository.DefaultBranch;
            }

            // Get base branch reference and commit
            var baseRef = await _githubClient.Git.Reference.Get(owner, repo, $"heads/{baseBranch}");
            var baseCommit = await _githubClient.Git.Commit.Get(owner, repo, baseRef.Object.Sha);
            var baseTreeSha = baseCommit.Tree.Sha;

            _logger.LogInformation(
                "Base branch {BaseBranch}: commit={CommitSha}, tree={TreeSha}",
                baseBranch,
                baseRef.Object.Sha,
                baseTreeSha);

            // Create new tree with all file changes
            var newTree = new NewTree { BaseTree = baseTreeSha };
            var filesProcessed = 0;

            foreach (var fileChange in autoFixResult.FileChanges)
            {
                try
                {
                    if (fileChange.ChangeType == FileChangeType.Modify || fileChange.ChangeType == FileChangeType.Create)
                    {
                        // Determine final content
                        string finalContent;

                        // Check if file exists on base branch for line changes
                        if (fileChange.LineChanges != null && fileChange.LineChanges.Count > 0)
                        {
                            IReadOnlyList<RepositoryContent>? existingFile = null;
                            try
                            {
                                existingFile = await _githubClient.Repository.Content.GetAllContentsByRef(
                                    owner,
                                    repo,
                                    fileChange.FilePath,
                                    baseBranch);
                            }
                            catch (NotFoundException)
                            {
                                _logger.LogError("Cannot apply line changes to non-existent file {FilePath}", fileChange.FilePath);
                                continue;
                            }

                            if (existingFile == null || existingFile.Count == 0)
                            {
                                _logger.LogError("Cannot apply line changes to non-existent file {FilePath}", fileChange.FilePath);
                                continue;
                            }

                            finalContent = ApplyLineChanges(existingFile[0].Content, fileChange.LineChanges);
                            _logger.LogInformation("Applied {Count} line changes to {FilePath}", fileChange.LineChanges.Count, fileChange.FilePath);
                        }
                        else
                        {
                            finalContent = fileChange.NewContent ?? string.Empty;
                        }

                        // Create blob for the file content
                        var blob = await _githubClient.Git.Blob.Create(owner, repo, new NewBlob
                        {
                            Content = finalContent,
                            Encoding = EncodingType.Utf8
                        });

                        _logger.LogInformation("Created blob {BlobSha} for {FilePath}", blob.Sha, fileChange.FilePath);

                        // Add blob to tree
                        newTree.Tree.Add(new NewTreeItem
                        {
                            Path = fileChange.FilePath,
                            Mode = "100644", // Regular file
                            Type = TreeType.Blob,
                            Sha = blob.Sha
                        });

                        filesProcessed++;
                    }
                    else if (fileChange.ChangeType == FileChangeType.Delete)
                    {
                        // Add deletion to tree (null SHA means delete)
                        newTree.Tree.Add(new NewTreeItem
                        {
                            Path = fileChange.FilePath,
                            Mode = "100644",
                            Type = TreeType.Blob,
                            Sha = null
                        });

                        filesProcessed++;
                        _logger.LogInformation("Marked {FilePath} for deletion", fileChange.FilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error preparing {FilePath} for commit", fileChange.FilePath);
                }
            }

            if (filesProcessed == 0)
            {
                _logger.LogWarning("No files were processed for commit");
                return null;
            }

            _logger.LogInformation("Creating Git tree with {FileCount} file change(s)", filesProcessed);

            // Create the tree
            var tree = await _githubClient.Git.Tree.Create(owner, repo, newTree);

            _logger.LogInformation("Created Git tree {TreeSha} with {FileCount} file(s)", tree.Sha, filesProcessed);

            // Build commit message
            var commitMessage = new StringBuilder();
            commitMessage.AppendLine($"Auto-fix: {autoFixResult.FixDescription}");
            commitMessage.AppendLine();
            commitMessage.AppendLine($"Files modified: {filesProcessed}");
            foreach (var fileChange in autoFixResult.FileChanges)
            {
                commitMessage.AppendLine($"  - {fileChange.ChangeType}: {fileChange.FilePath}");
            }

            // Create single commit with all changes
            var commit = await _githubClient.Git.Commit.Create(owner, repo, new NewCommit(
                message: commitMessage.ToString(),
                tree: tree.Sha,
                parents: new[] { baseRef.Object.Sha }
            ));

            _logger.LogInformation(
                "Created atomic commit {CommitSha} with {FileCount} file(s)",
                commit.Sha,
                filesProcessed);

            // Create new branch reference pointing to the commit
            var newBranch = await _githubClient.Git.Reference.Create(owner, repo, new NewReference(
                $"refs/heads/{branchName}",
                commit.Sha));

            _logger.LogInformation(
                "Successfully created branch {Branch} with atomic commit {CommitSha}. NO PR created yet.",
                branchName,
                commit.Sha);

            return commit.Sha;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating branch {Branch} and committing atomically", branchName);
            return null;
        }
    }

    /// <summary>
    /// Validates if a component string looks like a valid file path
    /// </summary>
    private bool IsValidFilePath(string component)
    {
        if (string.IsNullOrWhiteSpace(component))
            return false;

        // Must contain a file extension (e.g., .cs, .csproj, .json, .xml)
        var hasExtension = component.Contains('.') &&
                          component.LastIndexOf('.') < component.Length - 1 &&
                          !component.EndsWith('.');

        if (!hasExtension)
        {
            _logger.LogDebug("Component '{Component}' rejected: no file extension", component);
            return false;
        }

        // Reject if it contains terms that indicate it's a build step name
        var buildStepIndicators = new[] { "step", "stage", "job", "task", "phase" };
        var lowerComponent = component.ToLowerInvariant();

        foreach (var indicator in buildStepIndicators)
        {
            if (lowerComponent.Contains(indicator))
            {
                _logger.LogDebug("Component '{Component}' rejected: contains build step indicator '{Indicator}'",
                    component, indicator);
                return false;
            }
        }

        // Reject if it contains spaces (file paths shouldn't have spaces in CI/CD context)
        if (component.Contains(' '))
        {
            _logger.LogDebug("Component '{Component}' rejected: contains spaces", component);
            return false;
        }

        // Should look like a path (contains / or \ )
        var hasPathSeparator = component.Contains('/') || component.Contains('\\');

        // Valid extensions we expect in a .NET project
        var validExtensions = new[] { ".cs", ".csproj", ".json", ".xml", ".config", ".dll", ".exe", ".txt", ".md", ".yml", ".yaml" };
        var hasValidExtension = validExtensions.Any(ext => component.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

        return hasPathSeparator && hasValidExtension;
    }
}
