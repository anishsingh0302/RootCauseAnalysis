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

    public AutoFixService(
        IClaudeService claudeService,
        GitHubClient githubClient,
        ILogger<AutoFixService> logger)
    {
        _claudeService = claudeService;
        _githubClient = githubClient;
        _logger = logger;
    }

    public async Task<AutoFixResult> GenerateFixAsync(
        OrchestrationResult orchestrationResult,
        string owner,
        string repo,
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
            foreach (var component in components.Take(5)) // Limit to first 5 files
            {
                try
                {
                    var fileContent = await _githubClient.Repository.Content.GetAllContents(owner, repo, component);
                    if (fileContent.Count > 0)
                    {
                        fileContents[component] = fileContent[0].Content;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch file content for {Component}", component);
                }
            }

            // Generate fix using Claude
            var prompt = BuildFixGenerationPrompt(context.ToString(), fileContents, orchestrationResult);

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

    public async Task<int?> ApplyFixAndCreatePRAsync(
        AutoFixResult autoFixResult,
        string owner,
        string repo,
        string? baseBranch = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Applying auto-fix and creating PR in {Owner}/{Repo}",
                owner,
                repo);

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

                        if (existingFile != null && existingFile.Count > 0)
                        {
                            // Update existing file
                            await _githubClient.Repository.Content.UpdateFile(
                                owner,
                                repo,
                                fileChange.FilePath,
                                new UpdateFileRequest(
                                    $"Auto-fix: {fileChange.ChangeReason}",
                                    fileChange.NewContent ?? string.Empty,
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
                                    fileChange.NewContent ?? string.Empty,
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

    private string BuildFixGenerationPrompt(string context, Dictionary<string, string> fileContents, OrchestrationResult orchestrationResult)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are an expert software engineer tasked with generating an automatic fix for a CI/CD pipeline failure.");
        prompt.AppendLine();
        prompt.AppendLine("## Failure Analysis");
        prompt.AppendLine(context);
        prompt.AppendLine();

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
        prompt.AppendLine("## Response Format");
        prompt.AppendLine("Respond with a JSON object in this exact format:");
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
        prompt.AppendLine("      \"newContent\": \"Full new content of the file\",");
        prompt.AppendLine("      \"changeReason\": \"Why this change fixes the issue\"");
        prompt.AppendLine("    }");
        prompt.AppendLine("  ]");
        prompt.AppendLine("}");
        prompt.AppendLine("```");

        return prompt.ToString();
    }

    private AutoFixResult ParseFixResponse(string response)
    {
        try
        {
            // Extract JSON from response (may be wrapped in markdown code blocks)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false) }
                };
                return JsonSerializer.Deserialize<AutoFixResult>(json, options) ?? new AutoFixResult
                {
                    FixGenerated = false,
                    IsSafeToApply = false,
                    Reason = "Failed to parse fix response"
                };
            }

            return new AutoFixResult
            {
                FixGenerated = false,
                IsSafeToApply = false,
                Reason = "No valid JSON found in response"
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

    private string BuildPRBody(OrchestrationResult orchestrationResult, AutoFixResult fixResult)
    {
        var body = new StringBuilder();
        body.AppendLine("## Automatic Fix Generated");
        body.AppendLine();
        body.AppendLine($"**Confidence:** {fixResult.Confidence:P0}");
        body.AppendLine($"**Fix Description:** {fixResult.FixDescription}");
        body.AppendLine();
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
        }
        body.AppendLine();
        body.AppendLine("---");
        body.AppendLine($"*This PR was automatically generated by Agentic Pipeline (Orchestration ID: `{orchestrationResult.OrchestrationId}`)*");
        body.AppendLine();
        body.AppendLine("**⚠️ Please review carefully before merging!** This fix was generated automatically and may require adjustments.");

        return body.ToString();
    }
}
