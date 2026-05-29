using GitDelta.Core.Models;

namespace GitDelta.Core.Git;

/// <summary>
/// Read-only interface for querying a local git repository.
/// </summary>
public interface IGitReader
{
    /// <summary>Checks whether git is installed and meets the minimum required version (>= 2.30).</summary>
    Task<GitAvailability> CheckGitAsync(CancellationToken ct);

    /// <summary>
    /// Returns the absolute path of the repository root for the given starting directory,
    /// or <c>null</c> if the directory is not inside a git repository.
    /// </summary>
    Task<string?> FindRepositoryRootAsync(string startDirectory, CancellationToken ct);

    /// <summary>Returns a page of commit history from the given repository root.</summary>
    Task<IReadOnlyList<CommitInfo>> GetHistoryAsync(
        string repoRoot, int skip, int maxCount, CancellationToken ct);

    /// <summary>Returns the list of changed files for the given diff spec.</summary>
    Task<IReadOnlyList<ChangedFile>> GetChangedFilesAsync(
        string repoRoot, DiffSpec spec, CancellationToken ct);

    /// <summary>
    /// Returns the full file diff (with intra-line spans) for the given path and diff spec.
    /// </summary>
    Task<FileDiff> GetFileDiffAsync(
        string repoRoot, DiffSpec spec, string path, int contextLines, CancellationToken ct);
}
