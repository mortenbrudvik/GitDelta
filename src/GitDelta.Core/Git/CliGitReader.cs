using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GitDelta.Core.Diff;
using GitDelta.Core.Models;

namespace GitDelta.Core.Git;

/// <summary>
/// Reads repository information by shelling out to the git CLI via <see cref="IGitProcessRunner"/>
/// and parsing the output with the pure parsers in GitDelta.Core.Git.Parsing.
/// </summary>
public sealed partial class CliGitReader : IGitReader
{
    /// <summary>Files whose hunk count makes the textual diff too large to render; we truncate.</summary>
    public const int LargeFileHunkLineThreshold = 20_000;

    private readonly IGitProcessRunner _runner;
    private readonly IIntraLineDiffer _intraLineDiffer;

    public CliGitReader(IGitProcessRunner runner, IIntraLineDiffer intraLineDiffer)
    {
        _runner = runner;
        _intraLineDiffer = intraLineDiffer;
    }

    public async Task<GitAvailability> CheckGitAsync(CancellationToken ct)
    {
        GitResult result = await _runner
            .RunAsync(Directory.GetCurrentDirectory(), new[] { "--version" }, ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return new GitAvailability(IsInstalled: false, Version: null, MeetsMinimum: false);
        }

        string text = Encoding.UTF8.GetString(result.StdOut).Trim();
        Match m = GitVersionRegex().Match(text);
        if (!m.Success)
        {
            return new GitAvailability(IsInstalled: true, Version: null, MeetsMinimum: false);
        }

        string version = m.Groups["v"].Value;
        int major = int.Parse(m.Groups["major"].Value, CultureInfo.InvariantCulture);
        int minor = int.Parse(m.Groups["minor"].Value, CultureInfo.InvariantCulture);
        bool meets = major > 2 || (major == 2 && minor >= 30);

        return new GitAvailability(IsInstalled: true, Version: version, MeetsMinimum: meets);
    }

    public async Task<string?> FindRepositoryRootAsync(string startDirectory, CancellationToken ct)
    {
        GitResult result = await _runner
            .RunAsync(startDirectory, new[] { "rev-parse", "--show-toplevel" }, ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return null;
        }

        string top = Encoding.UTF8.GetString(result.StdOut).Trim();
        return string.IsNullOrEmpty(top) ? null : top;
    }

    public Task<IReadOnlyList<CommitInfo>> GetHistoryAsync(string repoRoot, int skip, int maxCount, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<ChangedFile>> GetChangedFilesAsync(string repoRoot, DiffSpec spec, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<FileDiff> GetFileDiffAsync(string repoRoot, DiffSpec spec, string path, int contextLines, CancellationToken ct) =>
        throw new NotImplementedException();

    [GeneratedRegex(@"git version (?<v>(?<major>\d+)\.(?<minor>\d+)\S*)", RegexOptions.CultureInvariant)]
    private static partial Regex GitVersionRegex();
}
