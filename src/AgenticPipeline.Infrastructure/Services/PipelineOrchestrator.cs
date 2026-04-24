using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Extensions;
using AgenticPipeline.Infrastructure.Plugins;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Orchestrates the complete failure analysis pipeline
/// </summary>
public class PipelineOrchestrator : IPipelineOrchestrator
{
    private readonly RcaPlugin _rcaPlugin;
    private readonly CommitAnalyzerPlugin _commitAnalyzerPlugin;
    private readonly IFailureRepository _failureRepository;
    private readonly ISimilarityService _similarityService;
    private readonly IPatternRecognitionService _patternRecognitionService;
    private readonly ILogger<PipelineOrchestrator> _logger;

    // In-memory store for orchestration status (in production, use Redis or database)
    private static readonly ConcurrentDictionary<Guid, OrchestrationResult> _orchestrations = new();

    public PipelineOrchestrator(
        RcaPlugin rcaPlugin,
        CommitAnalyzerPlugin commitAnalyzerPlugin,
        IFailureRepository failureRepository,
        ISimilarityService similarityService,
        IPatternRecognitionService patternRecognitionService,
        ILogger<PipelineOrchestrator> logger)
    {
        _rcaPlugin = rcaPlugin;
        _commitAnalyzerPlugin = commitAnalyzerPlugin;
        _failureRepository = failureRepository;
        _similarityService = similarityService;
        _patternRecognitionService = patternRecognitionService;
        _logger = logger;
    }

    public async Task<OrchestrationResult> ExecuteAsync(
        OrchestrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new OrchestrationResult
        {
            StartedAt = DateTime.UtcNow
        };

        _orchestrations[result.OrchestrationId] = result;

        try
        {
            _logger.LogInformation("Starting orchestration {OrchestrationId}", result.OrchestrationId);

            // Step 1: Perform RCA
            await ExecuteRcaAsync(result, request, cancellationToken);

            // Step 2: Analyze commits (if GitHub context provided)
            if (request.EnableCommitAnalysis &&
                !string.IsNullOrEmpty(request.GitHubOwner) &&
                !string.IsNullOrEmpty(request.GitHubRepo))
            {
                await ExecuteCommitAnalysisAsync(result, request, cancellationToken);
            }

            // Step 3: Search for similar historical failures
            await ExecuteSimilaritySearchAsync(result, request, cancellationToken);

            // Step 4: Pattern detection and assignment
            if (request.EnablePatternDetection)
            {
                await ExecutePatternDetectionAsync(result, request, cancellationToken);
            }

            // Step 5: Generate comprehensive report
            GenerateReport(result, request);

            result.Status = result.Errors.Count == 0
                ? OrchestrationStatus.Completed
                : OrchestrationStatus.PartialSuccess;

            _logger.LogInformation(
                "Orchestration {OrchestrationId} completed with status {Status}",
                result.OrchestrationId,
                result.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestration {OrchestrationId} failed", result.OrchestrationId);
            result.Status = OrchestrationStatus.Failed;
            result.Errors.Add($"Orchestration failed: {ex.Message}");
        }
        finally
        {
            result.CompletedAt = DateTime.UtcNow;
        }

        return result;
    }

    public Task<OrchestrationResult?> GetStatusAsync(Guid orchestrationId)
    {
        _orchestrations.TryGetValue(orchestrationId, out var result);
        return Task.FromResult(result);
    }

    private async Task ExecuteRcaAsync(
        OrchestrationResult result,
        OrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        var step = new ExecutionStep { StepName = "Root Cause Analysis" };
        result.ExecutionSteps.Add(step);

        try
        {
            _logger.LogInformation("Executing RCA for orchestration {OrchestrationId}", result.OrchestrationId);

            // RCA Plugin returns JSON string
            var rcaResultJson = await _rcaPlugin.AnalyzeFailureAsync(request.LogContent, cancellationToken);
            var rcaResult = JsonSerializer.Deserialize<RcaResult>(rcaResultJson);

            if (rcaResult == null)
            {
                throw new InvalidOperationException("Failed to deserialize RCA result");
            }

            // Convert RcaResult to FailureEntity and save (repository auto-generates embedding)
            var failureEntity = rcaResult.ToEntity(request.LogContent);
            var failure = await _failureRepository.SaveFailureAsync(failureEntity, cancellationToken);

            result.Failure = failure;
            result.TotalInputTokens += rcaResult.TokenUsage.InputTokens;
            result.TotalOutputTokens += rcaResult.TokenUsage.OutputTokens;

            step.Success = true;
            step.Metadata = new Dictionary<string, object>
            {
                ["FailureId"] = failure.Id,
                ["ErrorType"] = rcaResult.ErrorType,
                ["Confidence"] = rcaResult.ConfidenceScore,
                ["InputTokens"] = rcaResult.TokenUsage.InputTokens,
                ["OutputTokens"] = rcaResult.TokenUsage.OutputTokens
            };

            _logger.LogInformation(
                "RCA completed: FailureId={FailureId}, ErrorType={ErrorType}, Confidence={Confidence}",
                failure.Id,
                rcaResult.ErrorType,
                rcaResult.ConfidenceScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RCA step failed");
            step.Success = false;
            step.Error = ex.Message;
            result.Errors.Add($"RCA failed: {ex.Message}");
        }
        finally
        {
            step.CompletedAt = DateTime.UtcNow;
        }
    }

    private async Task ExecuteCommitAnalysisAsync(
        OrchestrationResult result,
        OrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        var step = new ExecutionStep { StepName = "Commit Analysis" };
        result.ExecutionSteps.Add(step);

        try
        {
            _logger.LogInformation("Executing commit analysis for orchestration {OrchestrationId}", result.OrchestrationId);

            // Get recent commits
            var commits = await _commitAnalyzerPlugin.GetRecentCommitsAsync(
                request.GitHubOwner!,
                request.GitHubRepo!,
                request.CommitLookbackDays,
                cancellationToken);

            result.RelatedCommits = commits;

            // Correlate commits with failure
            if (result.Failure != null && commits.Count > 0)
            {
                var failureTime = request.FailureTimestamp ?? DateTime.UtcNow;
                var correlation = await _commitAnalyzerPlugin.CorrelateCommitsWithFailureAsync(
                    request.GitHubOwner!,
                    request.GitHubRepo!,
                    result.Failure.LogContent,
                    failureTime,
                    request.CommitLookbackDays,
                    cancellationToken);

                result.CommitCorrelation = correlation;
            }

            step.Success = true;
            step.Metadata = new Dictionary<string, object>
            {
                ["CommitsFound"] = commits.Count,
                ["Repository"] = $"{request.GitHubOwner}/{request.GitHubRepo}"
            };

            _logger.LogInformation(
                "Commit analysis completed: Found {CommitCount} commits",
                commits.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Commit analysis step failed");
            step.Success = false;
            step.Error = ex.Message;
            result.Errors.Add($"Commit analysis failed: {ex.Message}");
        }
        finally
        {
            step.CompletedAt = DateTime.UtcNow;
        }
    }

    private async Task ExecuteSimilaritySearchAsync(
        OrchestrationResult result,
        OrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        var step = new ExecutionStep { StepName = "Similarity Search" };
        result.ExecutionSteps.Add(step);

        try
        {
            if (result.Failure?.Embedding == null)
            {
                _logger.LogWarning("Skipping similarity search - no embedding available");
                step.Success = true;
                step.Metadata = new Dictionary<string, object>
                {
                    ["Skipped"] = true,
                    ["Reason"] = "No embedding available"
                };
                step.CompletedAt = DateTime.UtcNow;
                return;
            }

            _logger.LogInformation("Executing similarity search for orchestration {OrchestrationId}", result.OrchestrationId);

            // Get all historical failures
            var allFailures = await _failureRepository.GetHistoryAsync(
                limit: 1000,
                offset: 0,
                errorType: null,
                cancellationToken);

            // Exclude the current failure from candidates
            var candidates = allFailures.Where(f => f.Id != result.Failure.Id).ToList();

            if (candidates.Count == 0)
            {
                _logger.LogInformation("No historical failures to compare against");
                step.Success = true;
                step.Metadata = new Dictionary<string, object>
                {
                    ["SimilarFailuresFound"] = 0
                };
                step.CompletedAt = DateTime.UtcNow;
                return;
            }

            // Find similar failures
            var similarResults = _similarityService.FindSimilarFailures(
                result.Failure,
                candidates,
                topN: request.SimilarFailuresCount,
                minSimilarity: request.MinSimilarity);

            result.SimilarFailures = similarResults.Select(sr => new SimilarFailure
            {
                FailureId = sr.Failure.Id,
                ErrorType = sr.Failure.ErrorType,
                RootCause = sr.Failure.RootCause,
                Similarity = sr.SimilarityScore,
                AnalyzedAt = sr.Failure.AnalyzedAt,
                Recommendations = JsonSerializer.Deserialize<List<string>>(
                    sr.Failure.RecommendationsJson)
            }).ToList();

            step.Success = true;
            step.Metadata = new Dictionary<string, object>
            {
                ["SimilarFailuresFound"] = result.SimilarFailures.Count,
                ["CandidatesSearched"] = candidates.Count
            };

            _logger.LogInformation(
                "Similarity search completed: Found {Count} similar failures",
                result.SimilarFailures.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Similarity search step failed");
            step.Success = false;
            step.Error = ex.Message;
            result.Errors.Add($"Similarity search failed: {ex.Message}");
        }
        finally
        {
            step.CompletedAt = DateTime.UtcNow;
        }
    }

    private async Task ExecutePatternDetectionAsync(
        OrchestrationResult result,
        OrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        var step = new ExecutionStep { StepName = "Pattern Detection" };
        result.ExecutionSteps.Add(step);

        try
        {
            if (result.Failure?.Embedding == null)
            {
                _logger.LogWarning("Skipping pattern detection - no embedding available");
                step.Success = true;
                step.Metadata = new Dictionary<string, object>
                {
                    ["Skipped"] = true,
                    ["Reason"] = "No embedding available"
                };
                step.CompletedAt = DateTime.UtcNow;
                return;
            }

            _logger.LogInformation("Executing pattern detection for orchestration {OrchestrationId}", result.OrchestrationId);

            // Get all failures with embeddings
            var allFailures = await _failureRepository.GetHistoryAsync(
                limit: 1000,
                offset: 0,
                errorType: null,
                cancellationToken);

            var failuresWithEmbeddings = allFailures
                .Where(f => f.Embedding != null && f.Embedding.Length > 0)
                .ToList();

            if (failuresWithEmbeddings.Count < 5)
            {
                _logger.LogInformation("Not enough failures for pattern detection (need at least 5)");
                step.Success = true;
                step.Metadata = new Dictionary<string, object>
                {
                    ["Skipped"] = true,
                    ["Reason"] = "Insufficient data",
                    ["FailuresWithEmbeddings"] = failuresWithEmbeddings.Count
                };
                step.CompletedAt = DateTime.UtcNow;
                return;
            }

            // Detect patterns using recent failures
            var recentFailures = failuresWithEmbeddings
                .OrderByDescending(f => f.AnalyzedAt)
                .Take(100) // Use last 100 failures for pattern detection
                .ToList();

            var optimalK = _patternRecognitionService.DetermineOptimalClusterCount(
                recentFailures,
                maxClusters: Math.Min(10, recentFailures.Count / 3));

            var patternResult = _patternRecognitionService.DetectPatterns(
                recentFailures,
                new ClusteringParameters
                {
                    ClusterCount = optimalK,
                    MinClusterSize = 2,
                    MaxIterations = 100,
                    RandomSeed = null // Use random initialization
                });

            // Assign current failure to a pattern
            if (patternResult.Patterns.Count > 0)
            {
                var assignment = _patternRecognitionService.AssignToPattern(
                    result.Failure,
                    patternResult.Patterns);

                if (assignment.HasValue)
                {
                    result.AssignedPattern = assignment.Value.pattern;
                    result.PatternSimilarity = assignment.Value.similarity;
                }
            }

            step.Success = true;
            step.Metadata = new Dictionary<string, object>
            {
                ["PatternsDetected"] = patternResult.Patterns.Count,
                ["OptimalK"] = optimalK,
                ["FailuresAnalyzed"] = recentFailures.Count,
                ["AssignedToPattern"] = result.AssignedPattern != null
            };

            _logger.LogInformation(
                "Pattern detection completed: Found {PatternCount} patterns, assigned={Assigned}",
                patternResult.Patterns.Count,
                result.AssignedPattern != null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pattern detection step failed");
            step.Success = false;
            step.Error = ex.Message;
            result.Errors.Add($"Pattern detection failed: {ex.Message}");
        }
        finally
        {
            step.CompletedAt = DateTime.UtcNow;
        }
    }

    private void GenerateReport(OrchestrationResult result, OrchestrationRequest request)
    {
        var report = new StringBuilder();

        report.AppendLine("# 🔍 Comprehensive Failure Analysis Report");
        report.AppendLine();
        report.AppendLine($"**Orchestration ID:** `{result.OrchestrationId}`");
        report.AppendLine($"**Analyzed At:** {result.StartedAt:yyyy-MM-dd HH:mm:ss UTC}");
        report.AppendLine($"**Duration:** {result.Duration?.TotalSeconds:F2}s");
        report.AppendLine($"**Status:** {result.Status}");
        if (!string.IsNullOrEmpty(request.Source))
        {
            report.AppendLine($"**Source:** {request.Source}");
        }
        report.AppendLine();

        // Root Cause Analysis
        if (result.Failure != null)
        {
            report.AppendLine("## 🎯 Root Cause Analysis");
            report.AppendLine();
            report.AppendLine($"**Error Type:** {result.Failure.ErrorType}");
            report.AppendLine($"**Confidence:** {result.Failure.ConfidenceScore:P0}");
            report.AppendLine();
            report.AppendLine("### Root Cause");
            report.AppendLine(result.Failure.RootCause);
            report.AppendLine();
            report.AppendLine("### Detailed Analysis");
            report.AppendLine(result.Failure.DetailedAnalysis);
            report.AppendLine();

            var affectedComponents = JsonSerializer.Deserialize<List<string>>(
                result.Failure.AffectedComponentsJson);
            if (affectedComponents != null && affectedComponents.Count > 0)
            {
                report.AppendLine("### Affected Components");
                foreach (var component in affectedComponents)
                {
                    report.AppendLine($"- {component}");
                }
                report.AppendLine();
            }

            var recommendations = JsonSerializer.Deserialize<List<string>>(
                result.Failure.RecommendationsJson);
            if (recommendations != null && recommendations.Count > 0)
            {
                report.AppendLine("### 💡 Recommendations");
                for (int i = 0; i < recommendations.Count; i++)
                {
                    report.AppendLine($"{i + 1}. {recommendations[i]}");
                }
                report.AppendLine();
            }
        }

        // Similar Failures
        if (result.SimilarFailures != null && result.SimilarFailures.Count > 0)
        {
            report.AppendLine("## 🔗 Similar Historical Failures");
            report.AppendLine();
            report.AppendLine("This failure is similar to the following past failures:");
            report.AppendLine();

            foreach (var similar in result.SimilarFailures.Take(5))
            {
                report.AppendLine($"### Failure #{similar.FailureId} ({similar.Similarity:P0} similar)");
                report.AppendLine($"**Error Type:** {similar.ErrorType}");
                report.AppendLine($"**Occurred:** {similar.AnalyzedAt:yyyy-MM-dd HH:mm}");
                report.AppendLine($"**Root Cause:** {similar.RootCause}");
                if (similar.Recommendations != null && similar.Recommendations.Count > 0)
                {
                    report.AppendLine("**What worked:**");
                    foreach (var rec in similar.Recommendations)
                    {
                        report.AppendLine($"- {rec}");
                    }
                }
                report.AppendLine();
            }
        }

        // Pattern Assignment
        if (result.AssignedPattern != null)
        {
            report.AppendLine("## 📊 Pattern Recognition");
            report.AppendLine();
            report.AppendLine($"This failure belongs to pattern: **{result.AssignedPattern.Name}**");
            report.AppendLine($"**Pattern Similarity:** {result.PatternSimilarity:P0}");
            report.AppendLine($"**Pattern Type:** {result.AssignedPattern.PrimaryErrorType}");
            report.AppendLine($"**Frequency:** {result.AssignedPattern.Frequency:F2} failures/day");
            report.AppendLine($"**Total Occurrences:** {result.AssignedPattern.FailureCount}");
            report.AppendLine();

            if (result.AssignedPattern.CommonRootCauses.Count > 0)
            {
                report.AppendLine("**Common Root Causes in this Pattern:**");
                foreach (var cause in result.AssignedPattern.CommonRootCauses.Take(3))
                {
                    report.AppendLine($"- {cause}");
                }
                report.AppendLine();
            }
        }

        // Commit Analysis
        if (result.RelatedCommits != null && result.RelatedCommits.Count > 0)
        {
            report.AppendLine("## 📝 Related Commits");
            report.AppendLine();
            report.AppendLine($"Found {result.RelatedCommits.Count} recent commits:");
            report.AppendLine();

            foreach (var commit in result.RelatedCommits.Take(5))
            {
                report.AppendLine($"- **{commit.Sha[..7]}** by {commit.Author} ({commit.CommitDate:yyyy-MM-dd HH:mm})");
                report.AppendLine($"  {commit.Message}");
            }
            report.AppendLine();

            if (!string.IsNullOrEmpty(result.CommitCorrelation))
            {
                report.AppendLine("### Commit Correlation Analysis");
                report.AppendLine(result.CommitCorrelation);
                report.AppendLine();
            }
        }

        // Execution Summary
        report.AppendLine("## ⚙️ Execution Summary");
        report.AppendLine();
        report.AppendLine("| Step | Status | Duration |");
        report.AppendLine("|------|--------|----------|");
        foreach (var step in result.ExecutionSteps)
        {
            var status = step.Success ? "✅ Success" : "❌ Failed";
            var duration = step.Duration?.TotalSeconds.ToString("F2") ?? "N/A";
            report.AppendLine($"| {step.StepName} | {status} | {duration}s |");
        }
        report.AppendLine();

        // Token Usage
        report.AppendLine($"**Total Token Usage:** {result.TotalInputTokens + result.TotalOutputTokens:N0} " +
                         $"({result.TotalInputTokens:N0} input + {result.TotalOutputTokens:N0} output)");
        report.AppendLine();

        // Errors
        if (result.Errors.Count > 0)
        {
            report.AppendLine("## ⚠️ Errors");
            report.AppendLine();
            foreach (var error in result.Errors)
            {
                report.AppendLine($"- {error}");
            }
            report.AppendLine();
        }

        report.AppendLine("---");
        report.AppendLine($"*Generated by Agentic Pipeline v4.0*");

        result.Report = report.ToString();
    }
}
