using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

Console.WriteLine("=== GitHub Connection Test ===\n");

// Create logger
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<GitHubService>();

// Create GitHub settings (no token for public API test)
var settings = Options.Create(new GitHubSettings(
    PersonalAccessToken: "", // Empty = unauthenticated (lower rate limits)
    DefaultOwner: "",
    DefaultRepo: ""
));

// Create GitHub service
var githubService = new GitHubService(settings, logger);

try
{
    Console.WriteLine("Testing GitHub API connection...");
    Console.WriteLine("Fetching recent commits from microsoft/dotnet repository...\n");

    // Test: Get recent commits from a public repository
    var commits = await githubService.GetRecentCommitsAsync("microsoft", "dotnet", 5);

    Console.WriteLine($"✅ Successfully retrieved {commits.Count} commits:\n");

    foreach (var commit in commits)
    {
        Console.WriteLine($"  📝 {commit.Sha[..7]} - {commit.Message.Split('\n')[0]}");
        Console.WriteLine($"     👤 {commit.Author} ({commit.AuthorEmail})");
        Console.WriteLine($"     📅 {commit.CommitDate:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"     🔗 {commit.Url}\n");
    }

    // Test: Get detailed commit info
    if (commits.Count > 0)
    {
        var firstCommit = commits[0];
        Console.WriteLine($"\nFetching detailed info for commit: {firstCommit.Sha[..7]}...\n");

        var detailedCommit = await githubService.GetCommitDetailsAsync("microsoft", "dotnet", firstCommit.Sha);

        Console.WriteLine($"✅ Commit Details:");
        Console.WriteLine($"   📊 Files Changed: {detailedCommit.FilesChanged.Count}");
        Console.WriteLine($"   ➕ Additions: {detailedCommit.Additions}");
        Console.WriteLine($"   ➖ Deletions: {detailedCommit.Deletions}\n");

        if (detailedCommit.FilesChanged.Count > 0)
        {
            Console.WriteLine("   Files:");
            foreach (var file in detailedCommit.FilesChanged.Take(5))
            {
                Console.WriteLine($"     - {file.FileName} ({file.Status}) +{file.Additions}/-{file.Deletions}");
            }

            if (detailedCommit.FilesChanged.Count > 5)
            {
                Console.WriteLine($"     ... and {detailedCommit.FilesChanged.Count - 5} more files");
            }
        }
    }

    Console.WriteLine("\n✅ GitHub connection test PASSED!");
    Console.WriteLine("\n📌 Note: Running without authentication (lower rate limits)");
    Console.WriteLine("   To use authentication, set PersonalAccessToken in appsettings.json");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ GitHub connection test FAILED!");
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
    return 1;
}

return 0;
