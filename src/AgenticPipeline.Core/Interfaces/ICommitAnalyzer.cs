namespace AgenticPipeline.Core.Interfaces;

/// <summary>
/// Interface for analyzing GitHub commits to correlate with pipeline failures.
/// </summary>
public interface ICommitAnalyzer
{
    /// <summary>
    /// Gets recent commits from a GitHub repository.
    /// </summary>
    /// <param name="owner">Repository owner (username or organization)</param>
    /// <param name="repo">Repository name</param>
    /// <param name="count">Number of recent commits to retrieve (default 10)</param>
    /// <returns>List of commit information</returns>
    Task<List<CommitInfo>> GetRecentCommitsAsync(string owner, string repo, int count = 10);

    /// <summary>
    /// Gets commit details including files changed and diffs.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="commitSha">Commit SHA</param>
    /// <returns>Detailed commit information</returns>
    Task<CommitInfo> GetCommitDetailsAsync(string owner, string repo, string commitSha);

    /// <summary>
    /// Finds commits within a specific time range.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="since">Start of time range</param>
    /// <param name="until">End of time range</param>
    /// <returns>List of commits in the time range</returns>
    Task<List<CommitInfo>> GetCommitsByTimeRangeAsync(string owner, string repo, DateTime since, DateTime until);
}

/// <summary>
/// Represents commit information from GitHub.
/// </summary>
/// <param name="Sha">Commit SHA identifier</param>
/// <param name="Message">Commit message</param>
/// <param name="Author">Author name</param>
/// <param name="AuthorEmail">Author email</param>
/// <param name="CommitDate">Commit timestamp</param>
/// <param name="FilesChanged">List of files modified in the commit</param>
/// <param name="Additions">Total lines added</param>
/// <param name="Deletions">Total lines deleted</param>
/// <param name="Url">GitHub URL to the commit</param>
public record CommitInfo(
    string Sha,
    string Message,
    string Author,
    string AuthorEmail,
    DateTime CommitDate,
    List<FileChange> FilesChanged,
    int Additions,
    int Deletions,
    string Url
);

/// <summary>
/// Represents a file change in a commit.
/// </summary>
/// <param name="FileName">Name of the file</param>
/// <param name="Status">Change status (added, modified, removed, renamed)</param>
/// <param name="Additions">Lines added</param>
/// <param name="Deletions">Lines deleted</param>
/// <param name="Patch">Diff patch content (optional)</param>
public record FileChange(
    string FileName,
    string Status,
    int Additions,
    int Deletions,
    string? Patch
);
