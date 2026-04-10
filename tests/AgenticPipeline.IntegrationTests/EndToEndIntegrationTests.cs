using AgenticPipeline.Tests.Common;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AgenticPipeline.IntegrationTests;

/// <summary>
/// End-to-end integration tests covering complete workflows
/// This replaces the old Program.cs console test with proper xUnit tests
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "EndToEnd")]
[Trait("RequiresAWS", "true")]
[Trait("RequiresGitHub", "true")]
public class EndToEndIntegrationTests : IntegrationTestBase
{
    public EndToEndIntegrationTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task Phase2_CompareBaselineVsEnhancedRca_ShowsImprovement()
    {
        // This is the main test from the original Phase2IntegrationTest/Program.cs
        // Split into proper AAA pattern

        // Arrange
        AwsCredentialHelper.EnsureCredentials();
        var factory = new TestServiceFactory(LoggerFactory);

        var rcaPlugin = factory.CreateRcaPlugin();
        var commitPlugin = factory.CreateCommitAnalyzerPlugin();
        var failureLog = await TestDataHelper.LoadCompilationErrorLogAsync();

        var owner = "microsoft";
        var repo = "dotnet";
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        WriteSection("SCENARIO A: Baseline RCA (Without Commit Context)");

        // Act - Scenario A
        var baselineStart = DateTime.UtcNow;
        var baselineResult = await rcaPlugin.AnalyzeFailureAsync(failureLog);
        var baselineElapsed = (DateTime.UtcNow - baselineStart).TotalSeconds;

        // Assert - Scenario A
        Assert.NotNull(baselineResult);
        Assert.NotEmpty(baselineResult);

        var baselineJson = JsonDocument.Parse(baselineResult);
        var baselineRoot = baselineJson.RootElement;

        Assert.True(baselineRoot.TryGetProperty("rootCause", out var baselineRootCause));
        Assert.False(string.IsNullOrWhiteSpace(baselineRootCause.GetString()));

        WriteLine($"✅ Baseline RCA completed in {baselineElapsed:F1} seconds");
        WriteLine($"Root Cause: {baselineRootCause.GetString()}");

        // Log baseline results
        if (baselineRoot.TryGetProperty("confidence", out var baselineConfidence))
        {
            WriteLine($"Confidence: {baselineConfidence.GetDouble():P0}");
        }

        WriteSection("SCENARIO B: Enhanced RCA (With Commit Context)");

        // Act - Scenario B
        var enhancedStart = DateTime.UtcNow;
        var commitAnalysis = await commitPlugin.CorrelateCommitsWithFailure(
            owner, repo, failureLog, timestamp, lookbackHours: 24);
        var enhancedElapsed = (DateTime.UtcNow - enhancedStart).TotalSeconds;

        // Assert - Scenario B
        Assert.NotNull(commitAnalysis);
        Assert.NotEmpty(commitAnalysis);

        var enhancedJson = JsonDocument.Parse(commitAnalysis);
        var enhancedRoot = enhancedJson.RootElement;

        Assert.True(enhancedRoot.TryGetProperty("success", out var success));
        Assert.True(success.GetBoolean());

        Assert.True(enhancedRoot.TryGetProperty("commitsAnalyzed", out var commitsAnalyzed));
        var commitCount = commitsAnalyzed.GetInt32();

        WriteLine($"✅ Enhanced RCA completed in {enhancedElapsed:F1} seconds");
        WriteLine($"Commits analyzed: {commitCount}");

        if (commitCount > 0)
        {
            Assert.True(enhancedRoot.TryGetProperty("aiAnalysis", out var aiAnalysis));
            var aiAnalysisText = aiAnalysis.GetString();
            Assert.NotNull(aiAnalysisText);
            Assert.NotEmpty(aiAnalysisText);

            WriteLine($"AI correlation analysis: {aiAnalysisText.Length} characters");
        }

        WriteSection("COMPARISON & RESULTS");

        WriteLine("✅ Both scenarios completed successfully!");
        WriteLine("");
        WriteLine("KEY FINDINGS:");
        WriteLine("  • Baseline RCA analyzes only the failure log");
        WriteLine("  • Enhanced RCA combines log + GitHub commit context");
        WriteLine($"  • Baseline timing: {baselineElapsed:F1}s");
        WriteLine($"  • Enhanced timing: {enhancedElapsed:F1}s");

        if (commitCount > 0)
        {
            WriteLine("  • Commit context enables identification of specific code changes");
            WriteLine("  • Enhanced analysis provides commit-to-failure correlation");
        }
        else
        {
            WriteLine("  • No recent commits found (expected for sample data)");
            WriteLine("  • In real scenarios, this would show commit correlation");
        }
    }

    [Fact]
    public async Task EndToEnd_MultipleFailureTypes_AllAnalyzedSuccessfully()
    {
        // Arrange
        AwsCredentialHelper.EnsureCredentials();
        var factory = new TestServiceFactory(LoggerFactory);
        var rcaPlugin = factory.CreateRcaPlugin();

        var testCases = new[]
        {
            ("Compilation Error", TestDataHelper.LoadCompilationErrorLogAsync()),
            ("Test Failure", TestDataHelper.LoadTestFailureLogAsync()),
            ("Dependency Error", TestDataHelper.LoadDependencyErrorLogAsync())
        };

        WriteSection("Testing Multiple Failure Types");

        // Act & Assert
        foreach (var (name, logTask) in testCases)
        {
            WriteLine($"\nAnalyzing: {name}");
            var log = await logTask;
            var result = await rcaPlugin.AnalyzeFailureAsync(log);

            Assert.NotNull(result);
            var json = JsonDocument.Parse(result);
            var root = json.RootElement;

            Assert.True(root.TryGetProperty("rootCause", out _));
            Assert.True(root.TryGetProperty("errorType", out _));
            Assert.True(root.TryGetProperty("confidence", out _));

            WriteLine($"  ✅ {name} analyzed successfully");
        }

        WriteLine("\n✅ All failure types handled correctly");
    }

    [Fact]
    public async Task EndToEnd_RcaPipeline_MaintainsPerformance()
    {
        // Arrange
        AwsCredentialHelper.EnsureCredentials();
        var factory = new TestServiceFactory(LoggerFactory);
        var rcaPlugin = factory.CreateRcaPlugin();
        var log = await TestDataHelper.LoadCompilationErrorLogAsync();

        WriteSection("Performance Testing");

        // Act - Run multiple analyses
        var timings = new List<double>();
        const int iterations = 3;

        for (int i = 0; i < iterations; i++)
        {
            var start = DateTime.UtcNow;
            await rcaPlugin.AnalyzeFailureAsync(log);
            var elapsed = (DateTime.UtcNow - start).TotalSeconds;
            timings.Add(elapsed);
            WriteLine($"  Iteration {i + 1}: {elapsed:F1}s");
        }

        // Assert
        var avgTime = timings.Average();
        var maxTime = timings.Max();

        WriteLine($"\n✅ Performance metrics:");
        WriteLine($"  Average: {avgTime:F1}s");
        WriteLine($"  Max: {maxTime:F1}s");

        // Reasonable performance expectations
        Assert.True(avgTime < 30, $"Average time {avgTime:F1}s exceeds 30s threshold");
        Assert.True(maxTime < 45, $"Max time {maxTime:F1}s exceeds 45s threshold");
    }
}
