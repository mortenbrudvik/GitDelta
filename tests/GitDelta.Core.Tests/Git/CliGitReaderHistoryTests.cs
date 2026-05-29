using System.Text;
using GitDelta.Core.Diff;
using GitDelta.Core.Git;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Git;

public class CliGitReaderHistoryTests
{
    private const char US = '\u001f'; // 0x1f field separator
    private const char NUL = '\0'; // 0x00 record terminator

    private readonly IGitProcessRunner _runner = Substitute.For<IGitProcessRunner>();
    private readonly CliGitReader _sut;

    public CliGitReaderHistoryTests()
    {
        _sut = new CliGitReader(_runner, new DiffPlexIntraLineDiffer());
    }

    private static byte[] TwoCommitFixture()
    {
        // Fields: %H %P %an %ae %aI %cn %ce %cI %s %b  (10 fields), records NUL-terminated.
        string r1 = string.Join(US, new[]
        {
            "1111111111111111111111111111111111111111",
            "2222222222222222222222222222222222222222",
            "Ada Lovelace", "ada@example.com", "2026-05-29T10:00:00+00:00",
            "Ada Lovelace", "ada@example.com", "2026-05-29T10:00:00+00:00",
            "Add engine", "Body line one",
        }) + NUL;

        string r2 = string.Join(US, new[]
        {
            "2222222222222222222222222222222222222222",
            "", // root commit, no parents
            "Grace Hopper", "grace@example.com", "2026-05-28T09:00:00+00:00",
            "Grace Hopper", "grace@example.com", "2026-05-28T09:00:00+00:00",
            "Initial commit", "",
        }) + NUL;

        return Encoding.UTF8.GetBytes(r1 + r2);
    }

    [Fact]
    public async Task GetHistoryAsync_PassesPagedLogArgsAndRepoRoot()
    {
        IReadOnlyList<string>? captured = null;
        string? capturedDir = null;
        _runner.RunAsync(Arg.Do<string>(d => capturedDir = d),
                         Arg.Do<IReadOnlyList<string>>(a => captured = a),
                         Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(new GitResult(0, TwoCommitFixture(), string.Empty)));

        await _sut.GetHistoryAsync("C:/repo", skip: 40, maxCount: 20, CancellationToken.None);

        capturedDir.ShouldBe("C:/repo");
        captured.ShouldNotBeNull();
        captured![0].ShouldBe("log");
        captured.ShouldContain("-z");
        captured.ShouldContain("--skip=40");
        captured.ShouldContain("--max-count=20");
        captured.ShouldContain(a => a.StartsWith("--pretty=format:%H"));
    }

    [Fact]
    public async Task GetHistoryAsync_PassesCompletePrettyFormatIncludingParents()
    {
        IReadOnlyList<string>? captured = null;
        _runner.RunAsync(Arg.Any<string>(),
                         Arg.Do<IReadOnlyList<string>>(a => captured = a),
                         Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(new GitResult(0, TwoCommitFixture(), string.Empty)));

        await _sut.GetHistoryAsync("C:/repo", skip: 0, maxCount: 50, CancellationToken.None);

        captured.ShouldNotBeNull();
        // The full 10-field format must be sent verbatim. %P (parents) is load-bearing:
        // IsMerge/IsRoot are derived from the parent list, so it must never be dropped.
        captured!.ShouldContain(
            "--pretty=format:%H%x1f%P%x1f%an%x1f%ae%x1f%aI%x1f%cn%x1f%ce%x1f%cI%x1f%s%x1f%b");
        captured.ShouldContain(a => a.Contains("%P"));
    }

    [Fact]
    public async Task GetHistoryAsync_ParsesCommitsViaLogParser()
    {
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(new GitResult(0, TwoCommitFixture(), string.Empty)));

        var commits = await _sut.GetHistoryAsync("C:/repo", skip: 0, maxCount: 50, CancellationToken.None);

        commits.Count.ShouldBe(2);
        commits[0].Subject.ShouldBe("Add engine");
        commits[0].AuthorName.ShouldBe("Ada Lovelace");
        commits[0].IsMerge.ShouldBeFalse();
        commits[1].IsRoot.ShouldBeTrue();
        commits[1].Subject.ShouldBe("Initial commit");
    }

    [Fact]
    public async Task GetHistoryAsync_NonZeroExit_ReturnsEmptyList()
    {
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(new GitResult(128, Array.Empty<byte>(), "fatal: bad revision")));

        var commits = await _sut.GetHistoryAsync("C:/repo", skip: 0, maxCount: 50, CancellationToken.None);

        commits.ShouldBeEmpty();
    }
}
