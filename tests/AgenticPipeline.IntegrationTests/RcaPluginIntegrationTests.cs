using AgenticPipeline.Tests.Common;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AgenticPipeline.IntegrationTests;

/// <summary>
/// Integration tests for RCA Plugin with real Claude API
/// </summary>
[Trait("Category", "Integration")]
[Trait("RequiresAWS", "true")]
public class RcaPluginIntegrationTests : IntegrationTestBase
{
    public RcaPluginIntegrationTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task AnalyzeFailure_WithCompilationError_ReturnsStructuredAnalysis()
    {
        // Arrange
        AwsCredentialHelper.EnsureCredentials();
        var factory = new TestServiceFactory(LoggerFactory);
        var rcaPlugin = factory.CreateRcaPlugin();
        var logContent = await TestDataHelper.LoadCompilationErrorLogAsync();

        WriteSection("RCA Analysis - Compilation Error");

        // Act
        var startTime = DateTime.UtcNow;
        var result = await rcaPlugin.AnalyzeFailureAsync(logContent);
        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        // Verify required fields
        Assert.True(root.TryGetProperty("rootCause", out var rootCause));
        Assert.False(string.IsNullOrWhiteSpace(rootCause.GetString()));

        Assert.True(root.TryGetProperty("errorType", out var errorType));
        Assert.False(string.IsNullOrWhiteSpace(errorType.GetString()));

        Assert.True(root.TryGetProperty("confidence", out var confidence));
        var confidenceValue = confidence.GetDouble();
        Assert.InRange(confidenceValue, 0.0, 1.0);

        Assert.True(root.TryGetProperty("affectedComponents", out var components));
        Assert.True(components.GetArrayLength() > 0);

        Assert.True(root.TryGetProperty("recommendations", out var recommendations));
        Assert.True(recommendations.GetArrayLength() > 0);

        // Log results
        WriteLine($"✅ Analysis completed in {elapsed:F1} seconds");
        WriteLine($"Root Cause: {rootCause.GetString()}");
        WriteLine($"Error Type: {errorType.GetString()}");
        WriteLine($"Confidence: {confidenceValue:P0}");
        WriteLine($"Components: {components.GetArrayLength()}");
        WriteLine($"Recommendations: {recommendations.GetArrayLength()}");
    }

    [Fact]
    public async Task AnalyzeFailure_WithTestFailure_IdentifiesTestErrorType()
    {
        // Arrange
        AwsCredentialHelper.EnsureCredentials();
        var factory = new TestServiceFactory(LoggerFactory);
        var rcaPlugin = factory.CreateRcaPlugin();
        var logContent = await TestDataHelper.LoadTestFailureLogAsync();

        WriteSection("RCA Analysis - Test Failure");

        // Act
        var result = await rcaPlugin.AnalyzeFailureAsync(logContent);

        // Assert
        Assert.NotNull(result);
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("errorType", out var errorType));
        var errorTypeValue = errorType.GetString();

        // Should identify this as a test-related error
        Assert.Contains("test", errorTypeValue, StringComparison.OrdinalIgnoreCase);

        WriteLine($"✅ Correctly identified error type: {errorTypeValue}");
    }

    [Fact]
    public async Task AnalyzeFailure_WithDependencyError_ReturnsHighConfidence()
    {
        // Arrange
        AwsCredentialHelper.EnsureCredentials();
        var factory = new TestServiceFactory(LoggerFactory);
        var rcaPlugin = factory.CreateRcaPlugin();
        var logContent = await TestDataHelper.LoadDependencyErrorLogAsync();

        WriteSection("RCA Analysis - Dependency Error");

        // Act
        var result = await rcaPlugin.AnalyzeFailureAsync(logContent);

        // Assert
        Assert.NotNull(result);
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("confidence", out var confidence));
        var confidenceValue = confidence.GetDouble();

        // Dependency errors are usually clear and should have high confidence
        Assert.True(confidenceValue >= 0.7, $"Expected confidence >= 0.7, got {confidenceValue:P0}");

        WriteLine($"✅ High confidence analysis: {confidenceValue:P0}");
    }

    [Fact]
    public async Task AnalyzeFailure_TracksTokenUsage()
    {
        // Arrange
        AwsCredentialHelper.EnsureCredentials();
        var factory = new TestServiceFactory(LoggerFactory);
        var rcaPlugin = factory.CreateRcaPlugin();
        var logContent = await TestDataHelper.LoadCompilationErrorLogAsync();

        WriteSection("Token Usage Tracking");

        // Act
        var result = await rcaPlugin.AnalyzeFailureAsync(logContent);

        // Assert
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("tokenUsage", out var tokenUsage));
        Assert.True(tokenUsage.TryGetProperty("inputTokens", out var inputTokens));
        Assert.True(tokenUsage.TryGetProperty("outputTokens", out var outputTokens));
        Assert.True(tokenUsage.TryGetProperty("totalTokens", out var totalTokens));

        var inputCount = inputTokens.GetInt32();
        var outputCount = outputTokens.GetInt32();
        var totalCount = totalTokens.GetInt32();

        Assert.True(inputCount > 0, "Input tokens should be > 0");
        Assert.True(outputCount > 0, "Output tokens should be > 0");
        Assert.Equal(inputCount + outputCount, totalCount);

        WriteLine($"✅ Token usage: {inputCount} input + {outputCount} output = {totalCount} total");
    }
}
