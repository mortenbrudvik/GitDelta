using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GitDelta.Core.Diff;
using GitDelta.Core.Git.Parsing;
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

    public async Task<IReadOnlyList<CommitInfo>> GetHistoryAsync(
        string repoRoot, int skip, int maxCount, CancellationToken ct)
    {
        const string pretty =
            "--pretty=format:%H%x1f%P%x1f%an%x1f%ae%x1f%aI%x1f%cn%x1f%ce%x1f%cI%x1f%s%x1f%b";

        var args = new[]
        {
            "log",
            pretty,
            "-z",
            "--skip=" + skip.ToString(CultureInfo.InvariantCulture),
            "--max-count=" + maxCount.ToString(CultureInfo.InvariantCulture),
        };

        GitResult result = await _runner.RunAsync(repoRoot, args, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return Array.Empty<CommitInfo>();
        }

        return GitLogParser.Parse(result.StdOut);
    }

    public async Task<IReadOnlyList<ChangedFile>> GetChangedFilesAsync(
        string repoRoot, DiffSpec spec, CancellationToken ct)
    {
        bool isRoot = spec is DiffSpec.CommitVsParent c
            && await IsRootCommitAsync(repoRoot, c.Sha, ct).ConfigureAwait(false);

        IReadOnlyList<string> refs = DiffSpecArgs.ToDiffArgs(spec, isRoot);

        var numstatArgs = new List<string> { "diff", "--numstat", "-z" };
        numstatArgs.AddRange(refs);

        var nameStatusArgs = new List<string> { "diff", "--name-status", "-z", "-M", "-C" };
        nameStatusArgs.AddRange(refs);

        GitResult numstatResult = await _runner.RunAsync(repoRoot, numstatArgs, ct).ConfigureAwait(false);
        GitResult nameStatusResult = await _runner.RunAsync(repoRoot, nameStatusArgs, ct).ConfigureAwait(false);

        IReadOnlyList<NumstatEntry> numstat = numstatResult.Success
            ? NumstatParser.Parse(numstatResult.StdOut)
            : Array.Empty<NumstatEntry>();

        IReadOnlyList<NameStatusEntry> nameStatus = nameStatusResult.Success
            ? NameStatusParser.Parse(nameStatusResult.StdOut)
            : Array.Empty<NameStatusEntry>();

        IReadOnlyList<ChangedFile> extra = Array.Empty<ChangedFile>();
        if (spec is DiffSpec.WorkingTreeVsHead)
        {
            GitResult statusResult = await _runner
                .RunAsync(repoRoot, new[] { "status", "--porcelain=v2", "-z" }, ct)
                .ConfigureAwait(false);

            if (statusResult.Success)
            {
                extra = StatusPorcelainV2Parser.Parse(statusResult.StdOut);
            }
        }

        return ChangedFileMerge.Merge(numstat, nameStatus, extra);
    }

    private async Task<bool> IsRootCommitAsync(string repoRoot, string sha, CancellationToken ct)
    {
        // 'rev-list --count <sha>^' fails when the commit has no parent (i.e. it is a root commit).
        GitResult result = await _runner
            .RunAsync(repoRoot, new[] { "rev-list", "--count", sha + "^" }, ct)
            .ConfigureAwait(false);

        return !result.Success;
    }

    public async Task<FileDiff> GetFileDiffAsync(
        string repoRoot, DiffSpec spec, string path, int contextLines, CancellationToken ct)
    {
        bool isRoot = spec is DiffSpec.CommitVsParent c
            && await IsRootCommitAsync(repoRoot, c.Sha, ct).ConfigureAwait(false);

        IReadOnlyList<string> refs = DiffSpecArgs.ToDiffArgs(spec, isRoot);

        // Resolve the ChangedFile metadata (Kind/counts/binary) for this path.
        IReadOnlyList<ChangedFile> changed =
            await GetChangedFilesAsync(repoRoot, spec, ct).ConfigureAwait(false);
        ChangedFile file = changed.FirstOrDefault(f => f.Path == path)
            ?? new ChangedFile(path, OldPath: null, ChangeKind.Modified,
                               AddedLines: null, DeletedLines: null, IsBinary: false);

        var diffArgs = new List<string> { "diff", "-U" + contextLines.ToString(CultureInfo.InvariantCulture), "-M", "-C" };
        diffArgs.AddRange(refs);
        diffArgs.Add("--");
        diffArgs.Add(path);

        GitResult result = await _runner.RunAsync(repoRoot, diffArgs, ct).ConfigureAwait(false);
        string diffText = result.Success ? Encoding.UTF8.GetString(result.StdOut) : string.Empty;

        // Binary: git emits a "Binary files ... differ" line and no hunks.
        if (file.IsBinary || diffText.Contains("Binary files ", StringComparison.Ordinal))
        {
            return new FileDiff(file with { IsBinary = true }, Array.Empty<DiffHunk>(),
                                IsBinary: true, IsTruncated: false);
        }

        IReadOnlyList<DiffHunk> hunks = UnifiedDiffParser.Parse(diffText);

        int totalLines = 0;
        foreach (DiffHunk h in hunks)
        {
            totalLines += h.Lines.Count;
        }

        if (totalLines > LargeFileHunkLineThreshold)
        {
            return new FileDiff(file, Array.Empty<DiffHunk>(), IsBinary: false, IsTruncated: true);
        }

        var diff = new FileDiff(file, hunks, IsBinary: false, IsTruncated: false);
        return IntraLineEnricher.Enrich(diff, _intraLineDiffer);
    }

    [GeneratedRegex(@"git version (?<v>(?<major>\d+)\.(?<minor>\d+)\S*)", RegexOptions.CultureInvariant)]
    private static partial Regex GitVersionRegex();
}
