using AgenticPipeline.Tests.Common;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AgenticPipeline.IntegrationTests;

/// <summary>
/// Integration tests for Commit Analyzer Plugin
/// </summary>
[Trait("Category", "Integration")]
[Trait("RequiresAWS", "true")]
[Trait("RequiresGitHub", "true")]
public class CommitAnalyzerPluginIntegrationTests : IntegrationTestBase
{
    public CommitAnalyzerPluginIntegrationTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task AnalyzeCommitContext_WithValidRepo_ReturnsCommitSummary()
    {
        // Arrange
        var factory = new TestServiceFactory(LoggerFactory);
        var plugin = factory.CreateCommitAnalyzerPlugin();

        // Use configured defaults (anishsingh0302/Semantic-Kernel)
        var owner = Environment.GetEnvironmentVariable("GitHub__DefaultOwner") ?? "anishsingh0302";
        var repo = Environment.GetEnvironmentVariable("GitHub__DefaultRepo") ?? "Semantic-Kernel";
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var lookbackHours = 720; // 7 days

        WriteSection($"Analyzing Commit Context from {owner}/{repo}");

        // Act
        var result = await plugin.AnalyzeCommitContext(owner, repo, timestamp, lookbackHours);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("repository", out var repoName));
        Assert.Equal($"{owner}/{repo}", repoName.GetString());

        Assert.True(root.TryGetProperty("commitsFound", out var commitsFound));
        var commitCount = commitsFound.GetInt32();
        Assert.True(commitCount >= 0);

        WriteLine($"✅ Commit context analysis complete");
        WriteLine($"  Repository: {repoName.GetString()}");
        WriteLine($"  Commits found: {commitCount}");
        WriteLine($"  Lookback period: {lookbackHours} hours");

        if (commitCount > 0 && root.TryGetProperty("commits", out var commits))
        {
            WriteLine($"\n  Recent commits:");
            foreach (var commit in commits.EnumerateArray().Take(3))
            {
                var sha = commit.GetProperty("shortSha").GetString();
                var message = commit.GetProperty("message").GetString()?.Split('\n')[0];
                WriteLine($"    • {sha} - {message}");
            }
        }
    }

    [Fact]
    public async Task CorrelateCommitsWithFailure_WithFailureLog_ReturnsCorrelationAnalysis()
    {
        // Arrange
        AwsCredentialHelper.EnsureCredentials();
        var factory = new TestServiceFactory(LoggerFactory);
        var plugin = factory.CreateCommitAnalyzerPlugin();

        // Use configured defaults (anishsingh0302/Semantic-Kernel)
        var owner = Environment.GetEnvironmentVariable("GitHub__DefaultOwner") ?? "anishsingh0302";
        var repo = Environment.GetEnvironmentVariable("GitHub__DefaultRepo") ?? "Semantic-Kernel";
        var failureLog = await TestDataHelper.LoadCompilationErrorLogAsync();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var lookbackHours = 720;

        WriteSection($"Correlating Commits with Failure from {owner}/{repo}");

        // Act
        var startTime = DateTime.UtcNow;
        var result = await plugin.CorrelateCommitsWithFailure(
            owner, repo, failureLog, timestamp, lookbackHours);
        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("success", out var success));
        Assert.True(success.GetBoolean());

        Assert.True(root.TryGetProperty("commitsAnalyzed", out var commitsAnalyzed));
        var commitCount = commitsAnalyzed.GetInt32();

        WriteLine($"✅ Correlation analysis completed in {elapsed:F1} seconds");
        WriteLine($"  Commits analyzed: {commitCount}");

        if (commitCount > 0)
        {
            Assert.True(root.TryGetProperty("commitContext", out var commitContext));
            Assert.True(root.TryGetProperty("aiAnalysis", out var aiAnalysis));

            var aiAnalysisText = aiAnalysis.GetString();
            Assert.NotNull(aiAnalysisText);
            Assert.NotEmpty(aiAnalysisText);

            WriteLine($"  AI analysis length: {aiAnalysisText.Length} characters");

            // Try to parse AI analysis as JSON
            try
            {
                var aiJson = JsonDocument.Parse(aiAnalysisText);
                var aiRoot = aiJson.RootElement;

                if (aiRoot.TryGetProperty("suspectedCommits", out var suspected))
                {
                    var suspectedCount = suspected.GetArrayLength();
                    WriteLine($"  Suspected commits identified: {suspectedCount}");
                }
            }
            catch
            {
                // AI response might not be JSON, that's okay
                WriteLine($"  AI response is free-form text");
            }
        }
        else
        {
            WriteLine($"  ℹ️  No commits found in time range (expected for sample data)");
        }
    }

    [Fact]
    public async Task CorrelateCommitsWithFailure_WithNoCommitsInRange_HandlesGracefully()
    {
        // Arrange
        AwsCredentialHelper.EnsureCredentials();
        var factory = new TestServiceFactory(LoggerFactory);
        var plugin = factory.CreateCommitAnalyzerPlugin();

        // Use configured defaults (anishsingh0302/Semantic-Kernel)
        var owner = Environment.GetEnvironmentVariable("GitHub__DefaultOwner") ?? "anishsingh0302";
        var repo = Environment.GetEnvironmentVariable("GitHub__DefaultRepo") ?? "Semantic-Kernel";
        var failureLog = await TestDataHelper.LoadTestFailureLogAsync();
        // Use a timestamp far in the past
        var timestamp = DateTime.UtcNow.AddYears(-10).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var lookbackHours = 1; // Very short lookback

        WriteSection($"Testing Empty Commit Range Handling for {owner}/{repo}");

        // Act
        var result = await plugin.CorrelateCommitsWithFailure(
            owner, repo, failureLog, timestamp, lookbackHours);

        // Assert
        Assert.NotNull(result);
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("success", out var success));
        Assert.True(success.GetBoolean());

        Assert.True(root.TryGetProperty("commitsAnalyzed", out var commitsAnalyzed));
        Assert.Equal(0, commitsAnalyzed.GetInt32());

        WriteLine($"✅ Gracefully handled empty commit range");
    }

    [Fact]
    public async Task AnalyzeCommitContext_WithLongLookback_ReturnsRelevantCommits()
    {
        // Arrange
        var factory = new TestServiceFactory(LoggerFactory);
        var plugin = factory.CreateCommitAnalyzerPlugin();
        var owner = "anishsingh0302";
        var repo = "Semantic-Kernel";
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"); // Current time
        var lookbackHours = 720; // 30 days lookback

        WriteSection("Testing Long Lookback Period");

        // Act
        var result = await plugin.AnalyzeCommitContext(owner, repo, timestamp, lookbackHours);

        // Assert
        Assert.NotNull(result);
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("commitsFound", out var commitsFound));
        var commitCount = commitsFound.GetInt32();

        // With 30 days, we should find commits in an active repo
        Assert.True(commitCount > 0, "Expected to find commits in 30-day lookback");

        WriteLine($"✅ Found {commitCount} commits in 30-day period");
    }
}
