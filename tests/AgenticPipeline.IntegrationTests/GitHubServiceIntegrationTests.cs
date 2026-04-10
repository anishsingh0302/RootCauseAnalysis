using AgenticPipeline.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace AgenticPipeline.IntegrationTests;

/// <summary>
/// Integration tests for GitHub Service with real GitHub API
/// </summary>
[Trait("Category", "Integration")]
[Trait("RequiresGitHub", "true")]
public class GitHubServiceIntegrationTests : IntegrationTestBase
{
    public GitHubServiceIntegrationTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task GetRecentCommitsAsync_WithPublicRepo_ReturnsCommits()
    {
        // Arrange
        var factory = new TestServiceFactory(LoggerFactory);
        var githubService = factory.CreateGitHubService();

        // Use configured defaults (anishsingh0302/Semantic-Kernel)
        var owner = Environment.GetEnvironmentVariable("GitHub__DefaultOwner") ?? "anishsingh0302";
        var repo = Environment.GetEnvironmentVariable("GitHub__DefaultRepo") ?? "Semantic-Kernel";
        var count = 5;

        WriteSection($"Fetching Recent Commits from {owner}/{repo}");

        // Act
        var commits = await githubService.GetRecentCommitsAsync(owner, repo, count);

        // Assert
        Assert.NotNull(commits);
        Assert.NotEmpty(commits);
        Assert.True(commits.Count <= count, $"Expected <= {count} commits, got {commits.Count}");

        // Verify commit structure
        var firstCommit = commits[0];
        Assert.NotNull(firstCommit.Sha);
        Assert.NotEmpty(firstCommit.Sha);
        Assert.NotNull(firstCommit.Message);
        Assert.NotNull(firstCommit.Author);
        Assert.True(firstCommit.CommitDate < DateTime.UtcNow);

        WriteLine($"✅ Retrieved {commits.Count} commits");
        foreach (var commit in commits)
        {
            WriteLine($"  • {commit.Sha[..7]} - {commit.Message.Split('\n')[0]} (by {commit.Author})");
        }
    }

    [Fact]
    public async Task GetCommitDetailsAsync_WithValidCommit_ReturnsFileChanges()
    {
        // Arrange
        var factory = new TestServiceFactory(LoggerFactory);
        var githubService = factory.CreateGitHubService();

        // Use configured defaults (anishsingh0302/Semantic-Kernel)
        var owner = Environment.GetEnvironmentVariable("GitHub__DefaultOwner") ?? "anishsingh0302";
        var repo = Environment.GetEnvironmentVariable("GitHub__DefaultRepo") ?? "Semantic-Kernel";

        // First get a recent commit
        var recentCommits = await githubService.GetRecentCommitsAsync(owner, repo, 1);
        Assert.NotEmpty(recentCommits);
        var commitSha = recentCommits[0].Sha;

        WriteSection($"Fetching Commit Details: {commitSha[..7]}");

        // Act
        var commitDetails = await githubService.GetCommitDetailsAsync(owner, repo, commitSha);

        // Assert
        Assert.NotNull(commitDetails);
        Assert.Equal(commitSha, commitDetails.Sha);
        Assert.NotNull(commitDetails.FilesChanged);
        Assert.NotEmpty(commitDetails.FilesChanged);

        // Verify file change structure
        var firstFile = commitDetails.FilesChanged[0];
        Assert.NotNull(firstFile.FileName);
        Assert.NotEmpty(firstFile.FileName);

        WriteLine($"✅ Commit {commitSha[..7]} details:");
        WriteLine($"  Message: {commitDetails.Message.Split('\n')[0]}");
        WriteLine($"  Author: {commitDetails.Author}");
        WriteLine($"  Files changed: {commitDetails.FilesChanged.Count}");
        WriteLine($"  Total changes: +{commitDetails.Additions} -{commitDetails.Deletions}");

        WriteLine($"\n  File changes:");
        foreach (var file in commitDetails.FilesChanged.Take(3))
        {
            WriteLine($"    • {file.FileName} (+{file.Additions} -{file.Deletions})");
        }
    }

    [Fact]
    public async Task GetCommitsByTimeRangeAsync_WithDateRange_ReturnsFilteredCommits()
    {
        // Arrange
        var factory = new TestServiceFactory(LoggerFactory);
        var githubService = factory.CreateGitHubService();

        // Use configured defaults (anishsingh0302/Semantic-Kernel)
        var owner = Environment.GetEnvironmentVariable("GitHub__DefaultOwner") ?? "anishsingh0302";
        var repo = Environment.GetEnvironmentVariable("GitHub__DefaultRepo") ?? "Semantic-Kernel";
        var since = DateTime.UtcNow.AddDays(-14);
        var until = DateTime.UtcNow;

        WriteSection($"Fetching Commits in Last 7 Days from {owner}/{repo}");

        // Act
        var commits = await githubService.GetCommitsByTimeRangeAsync(owner, repo, since, until);

        // Assert
        Assert.NotNull(commits);

        // All commits should be within the time range
        foreach (var commit in commits)
        {
            Assert.True(commit.CommitDate >= since, $"Commit {commit.Sha[..7]} date {commit.CommitDate} is before {since}");
            Assert.True(commit.CommitDate <= until, $"Commit {commit.Sha[..7]} date {commit.CommitDate} is after {until}");
        }

        WriteLine($"✅ Retrieved {commits.Count} commits in date range");
        WriteLine($"  Date range: {since:yyyy-MM-dd} to {until:yyyy-MM-dd}");

        if (commits.Count > 0)
        {
            WriteLine($"\n  First commit: {commits[0].CommitDate:yyyy-MM-dd HH:mm}");
            WriteLine($"  Last commit: {commits[^1].CommitDate:yyyy-MM-dd HH:mm}");
        }
    }

    [Fact]
    public async Task GetCommitDetailsAsync_HandlesCommitWithNoFiles_Gracefully()
    {
        // Arrange
        var factory = new TestServiceFactory(LoggerFactory);
        var githubService = factory.CreateGitHubService();

        // Use configured defaults (anishsingh0302/Semantic-Kernel)
        var owner = Environment.GetEnvironmentVariable("GitHub__DefaultOwner") ?? "anishsingh0302";
        var repo = "AgenticPipeline";

        // Get recent commits
        var commits = await githubService.GetRecentCommitsAsync(owner, repo, 10);
        Assert.NotEmpty(commits);

        WriteSection("Testing Commit Details Handling");

        // Act & Assert - should not throw for any commit
        foreach (var commit in commits.Take(3))
        {
            var details = await githubService.GetCommitDetailsAsync(owner, repo, commit.Sha);
            Assert.NotNull(details);
            Assert.NotNull(details.FilesChanged); // Files list should never be null
            WriteLine($"✅ Handled commit {commit.Sha[..7]}: {details.FilesChanged.Count} files");
        }
    }
}
