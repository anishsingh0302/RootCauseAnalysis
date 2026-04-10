using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Infrastructure.Plugins;
using AgenticPipeline.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

Console.WriteLine("=== Commit Analyzer Plugin Test ===\n");

// Create logger factory
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var githubLogger = loggerFactory.CreateLogger<GitHubService>();
var pluginLogger = loggerFactory.CreateLogger<CommitAnalyzerPlugin>();

// Create GitHub settings (no token for public API)
var githubSettings = Options.Create(new GitHubSettings(
    PersonalAccessToken: "",
    DefaultOwner: "",
    DefaultRepo: ""
));

// Create services
var githubService = new GitHubService(githubSettings, githubLogger);

// Create a mock Claude service for testing (without AWS credentials)
var mockClaudeService = new MockClaudeService();

// Create CommitAnalyzerPlugin
var commitAnalyzerPlugin = new CommitAnalyzerPlugin(
    githubService,
    mockClaudeService,
    pluginLogger);

try
{
    Console.WriteLine("Test 1: Analyze Commit Context");
    Console.WriteLine("================================\n");
    Console.WriteLine("Analyzing commits from microsoft/dotnet repository...");
    Console.WriteLine("Time range: Last 24 hours from 2025-09-24T00:00:00Z\n");

    // Test AnalyzeCommitContext
    var result = await commitAnalyzerPlugin.AnalyzeCommitContext(
        owner: "microsoft",
        repo: "dotnet",
        failureTimestamp: "2025-09-24T00:00:00Z",
        lookbackHours: 24
    );

    // Parse and display results
    var jsonDoc = JsonDocument.Parse(result);
    var root = jsonDoc.RootElement;

    if (root.GetProperty("success").GetBoolean())
    {
        var commitsFound = root.GetProperty("commitsFound").GetInt32();
        Console.WriteLine($"✅ Success! Found {commitsFound} commit(s)\n");

        if (commitsFound > 0)
        {
            Console.WriteLine("Commits:");
            var commits = root.GetProperty("commits");
            foreach (var commit in commits.EnumerateArray())
            {
                var sha = commit.GetProperty("shortSha").GetString();
                var message = commit.GetProperty("message").GetString()?.Split('\n')[0];
                var author = commit.GetProperty("author").GetString();
                var date = commit.GetProperty("commitDate").GetString();
                var filesChanged = commit.GetProperty("filesChanged").GetInt32();

                Console.WriteLine($"  {sha} - {message}");
                Console.WriteLine($"           by {author} on {date}");
                Console.WriteLine($"           {filesChanged} file(s) changed\n");
            }

            // Show summary
            if (root.TryGetProperty("summary", out var summary))
            {
                Console.WriteLine("Summary:");
                Console.WriteLine(summary.GetString());
            }
        }
    }
    else
    {
        Console.WriteLine($"❌ Failed: {root.GetProperty("error").GetString()}");
    }

    Console.WriteLine("\n" + new string('=', 60) + "\n");
    Console.WriteLine("Test 2: Correlate Commits with Failure (Mock)");
    Console.WriteLine("===============================================\n");

    // Create a sample failure log
    var sampleFailureLog = @"
Build FAILED.

src/MyApp/Calculator.cs(15,20): error CS1002: ; expected
src/MyApp/Calculator.cs(23,5): error CS0103: The name 'result' does not exist in the current context

    2 Error(s)
    0 Warning(s)

Time Elapsed 00:00:05.34
";

    Console.WriteLine("Sample Failure Log:");
    Console.WriteLine(sampleFailureLog);
    Console.WriteLine("Correlating with commits...\n");

    var correlationResult = await commitAnalyzerPlugin.CorrelateCommitsWithFailure(
        owner: "microsoft",
        repo: "dotnet",
        failureLog: sampleFailureLog,
        failureTimestamp: "2025-09-24T00:00:00Z",
        lookbackHours: 24
    );

    var correlationDoc = JsonDocument.Parse(correlationResult);
    var correlationRoot = correlationDoc.RootElement;

    if (correlationRoot.GetProperty("success").GetBoolean())
    {
        var commitsAnalyzed = correlationRoot.GetProperty("commitsAnalyzed").GetInt32();
        Console.WriteLine($"✅ Success! Analyzed {commitsAnalyzed} commit(s)");

        if (correlationRoot.TryGetProperty("aiAnalysis", out var aiAnalysis))
        {
            Console.WriteLine("\nAI Analysis (Mock):");
            Console.WriteLine(aiAnalysis.GetString());
        }
    }
    else
    {
        Console.WriteLine($"❌ Failed: {correlationRoot.GetProperty("error").GetString()}");
    }

    Console.WriteLine("\n✅ All tests completed successfully!");
    Console.WriteLine("\n📌 Note: These tests use mock data. To test with real AWS Bedrock:");
    Console.WriteLine("   1. Set up AWS credentials");
    Console.WriteLine("   2. Replace MockClaudeService with real ClaudeService");
    Console.WriteLine("   3. Run the full integration test");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Test failed with error:");
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
    return 1;
}

return 0;

// Mock Claude Service for testing without AWS credentials
class MockClaudeService : IClaudeService
{
    public Task<string> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        // Return a mock analysis response
        var mockResponse = @"{
  ""suspectedCommits"": [
    {
      ""sha"": ""abc1234"",
      ""confidence"": 0.85,
      ""reason"": ""This commit modified Calculator.cs which matches the error location"",
      ""suspectedFiles"": [""src/MyApp/Calculator.cs""],
      ""evidence"": ""Error references Calculator.cs line 15 and 23, which were likely modified in this commit""
    }
  ],
  ""overallAnalysis"": ""The failure appears to be a syntax error in Calculator.cs. The error 'CS1002: ; expected' suggests a missing semicolon on line 15, and 'CS0103' on line 23 indicates an undefined variable 'result'."",
  ""recommendations"": [
    ""Review the changes in Calculator.cs from commit abc1234"",
    ""Add missing semicolon on line 15"",
    ""Ensure 'result' variable is declared before use on line 23"",
    ""Run local build to verify fixes before committing""
  ]
}";
        return Task.FromResult(mockResponse);
    }

    public Task<AgenticPipeline.Core.Models.ClaudeResponse> InvokeModelAsync(
        AgenticPipeline.Core.Models.ClaudeRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Use SendMessageAsync for this test");
    }
}
