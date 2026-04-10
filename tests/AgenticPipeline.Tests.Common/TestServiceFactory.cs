using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Plugins;
using AgenticPipeline.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticPipeline.Tests.Common;

/// <summary>
/// Factory for creating test service instances with consistent configuration
/// </summary>
public class TestServiceFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public TestServiceFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public ClaudeService CreateClaudeService(ClaudeSettings? settings = null)
    {
        var claudeSettings = settings ?? new ClaudeSettings(
            ModelId: "us.anthropic.claude-sonnet-4-6",
            Region: "us-west-2",
            MaxTokens: 4096,
            Temperature: 0.7
        );

        return new ClaudeService(Options.Create(claudeSettings));
    }

    public GitHubService CreateGitHubService(GitHubSettings? settings = null)
    {
        // Try to read from environment variables first (support both naming conventions), then use defaults
        var githubSettings = settings ?? new GitHubSettings(
            PersonalAccessToken: Environment.GetEnvironmentVariable("GitHub__PersonalAccessToken")
                ?? Environment.GetEnvironmentVariable("GITHUB_PAT")
                ?? "",
            DefaultOwner: Environment.GetEnvironmentVariable("GitHub__DefaultOwner")
                ?? Environment.GetEnvironmentVariable("GITHUB_OWNER")
                ?? "anishsingh0302",
            DefaultRepo: Environment.GetEnvironmentVariable("GitHub__DefaultRepo")
                ?? Environment.GetEnvironmentVariable("GITHUB_REPO")
                ?? "Semantic-Kernel"
        );

        return new GitHubService(
            Options.Create(githubSettings),
            _loggerFactory.CreateLogger<GitHubService>()
        );
    }

    public RcaPlugin CreateRcaPlugin(IClaudeService? claudeService = null)
    {
        var service = claudeService ?? CreateClaudeService();
        return new RcaPlugin(service);
    }

    public CommitAnalyzerPlugin CreateCommitAnalyzerPlugin(
        ICommitAnalyzer? githubService = null,
        IClaudeService? claudeService = null)
    {
        var github = githubService ?? CreateGitHubService();
        var claude = claudeService ?? CreateClaudeService();

        return new CommitAnalyzerPlugin(
            github,
            claude,
            _loggerFactory.CreateLogger<CommitAnalyzerPlugin>()
        );
    }
}
