using AgenticPipeline.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Service for interacting with GitHub API to analyze commits.
/// </summary>
public class GitHubService : ICommitAnalyzer
{
    private readonly GitHubClient _client;
    private readonly ILogger<GitHubService> _logger;
    private readonly GitHubSettings _settings;

    public GitHubService(IOptions<GitHubSettings> settings, ILogger<GitHubService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        // Initialize GitHub client
        _client = new GitHubClient(new ProductHeaderValue("AgenticPipeline"));

        // Authenticate if token provided
        if (!string.IsNullOrEmpty(_settings.PersonalAccessToken))
        {
            _client.Credentials = new Credentials(_settings.PersonalAccessToken);
            _logger.LogInformation("GitHub client initialized with authentication");
        }
        else
        {
            _logger.LogWarning("GitHub client initialized without authentication (rate limits will be lower)");
        }
    }

    /// <inheritdoc/>
    public async Task<List<Core.Interfaces.CommitInfo>> GetRecentCommitsAsync(string owner, string repo, int count = 10)
    {
        try
        {
            _logger.LogInformation("Fetching {Count} recent commits from {Owner}/{Repo}", count, owner, repo);

            var request = new CommitRequest
            {
                // Fetch recent commits
            };

            var commits = await _client.Repository.Commit.GetAll(owner, repo, request);
            var recentCommits = commits.Take(count).ToList();

            _logger.LogInformation("Retrieved {Count} commits", recentCommits.Count);

            return recentCommits.Select(MapToCommitInfo).ToList();
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "GitHub API error while fetching commits: {Message}", ex.Message);
            throw new InvalidOperationException($"Failed to fetch commits from GitHub: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching commits");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Core.Interfaces.CommitInfo> GetCommitDetailsAsync(string owner, string repo, string commitSha)
    {
        try
        {
            _logger.LogInformation("Fetching commit details for {Owner}/{Repo} @ {Sha}", owner, repo, commitSha);

            var commit = await _client.Repository.Commit.Get(owner, repo, commitSha);

            _logger.LogInformation("Retrieved commit: {Message}", commit.Commit.Message);

            return MapToCommitInfoDetailed(commit);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "GitHub API error while fetching commit details: {Message}", ex.Message);
            throw new InvalidOperationException($"Failed to fetch commit details from GitHub: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching commit details");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<Core.Interfaces.CommitInfo>> GetCommitsByTimeRangeAsync(string owner, string repo, DateTime since, DateTime until)
    {
        try
        {
            _logger.LogInformation(
                "Fetching commits from {Owner}/{Repo} between {Since} and {Until}",
                owner, repo, since, until);

            var request = new CommitRequest
            {
                Since = since,
                Until = until
            };

            var commits = await _client.Repository.Commit.GetAll(owner, repo, request);

            _logger.LogInformation("Retrieved {Count} commits in time range", commits.Count);

            return commits.Select(MapToCommitInfo).ToList();
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "GitHub API error while fetching commits by time range: {Message}", ex.Message);
            throw new InvalidOperationException($"Failed to fetch commits from GitHub: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching commits by time range");
            throw;
        }
    }

    /// <summary>
    /// Maps Octokit GitHubCommit to CommitInfo (basic info).
    /// </summary>
    private Core.Interfaces.CommitInfo MapToCommitInfo(GitHubCommit commit)
    {
        return new Core.Interfaces.CommitInfo(
            Sha: commit.Sha,
            Message: commit.Commit.Message,
            Author: commit.Commit.Author.Name,
            AuthorEmail: commit.Commit.Author.Email,
            CommitDate: commit.Commit.Author.Date.DateTime,
            FilesChanged: new List<Core.Interfaces.FileChange>(), // Not included in list view
            Additions: 0, // Not available in list view
            Deletions: 0, // Not available in list view
            Url: commit.HtmlUrl
        );
    }

    /// <summary>
    /// Maps Octokit GitHubCommit to CommitInfo (detailed info with files).
    /// </summary>
    private Core.Interfaces.CommitInfo MapToCommitInfoDetailed(GitHubCommit commit)
    {
        var filesChanged = commit.Files?.Select(f => new Core.Interfaces.FileChange(
            FileName: f.Filename,
            Status: f.Status,
            Additions: f.Additions,
            Deletions: f.Deletions,
            Patch: f.Patch
        )).ToList() ?? new List<Core.Interfaces.FileChange>();

        return new Core.Interfaces.CommitInfo(
            Sha: commit.Sha,
            Message: commit.Commit.Message,
            Author: commit.Commit.Author.Name,
            AuthorEmail: commit.Commit.Author.Email,
            CommitDate: commit.Commit.Author.Date.DateTime,
            FilesChanged: filesChanged,
            Additions: commit.Stats?.Additions ?? 0,
            Deletions: commit.Stats?.Deletions ?? 0,
            Url: commit.HtmlUrl
        );
    }
}

/// <summary>
/// Configuration settings for GitHub integration.
/// </summary>
/// <param name="PersonalAccessToken">GitHub Personal Access Token for authentication</param>
/// <param name="DefaultOwner">Default repository owner (optional)</param>
/// <param name="DefaultRepo">Default repository name (optional)</param>
public record GitHubSettings(
    string PersonalAccessToken = "",
    string DefaultOwner = "",
    string DefaultRepo = "")
{
    /// <summary>
    /// Default parameterless constructor for Options pattern
    /// </summary>
    public GitHubSettings() : this(
        PersonalAccessToken: "",
        DefaultOwner: "",
        DefaultRepo: "")
    {
    }
};
