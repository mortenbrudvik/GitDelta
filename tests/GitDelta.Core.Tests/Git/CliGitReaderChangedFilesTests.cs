using System.Text;
using GitDelta.Core.Diff;
using GitDelta.Core.Git;
using GitDelta.Core.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Git;

public class CliGitReaderChangedFilesTests
{
    private const string NUL = "\0";

    private readonly IGitProcessRunner _runner = Substitute.For<IGitProcessRunner>();
    private readonly CliGitReader _sut;

    public CliGitReaderChangedFilesTests()
    {
        _sut = new CliGitReader(_runner, new DiffPlexIntraLineDiffer());
    }

    // Routes a canned GitResult based on the first git subcommand + a marker arg.
    private void Route(Func<IReadOnlyList<string>, GitResult> selector) =>
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
               .Returns(ci => Task.FromResult(selector(ci.ArgAt<IReadOnlyList<string>>(1))));

    private static GitResult Ok(string s) => new(0, Encoding.UTF8.GetBytes(s), string.Empty);
    private static GitResult Ok(byte[] b) => new(0, b, string.Empty);

    [Fact]
    public async Task GetChangedFilesAsync_TwoCommits_MergesNumstatAndNameStatus()
    {
        // numstat -z: "added\tdeleted\tpath\0"
        string numstat = "3\t1\tsrc/app.cs" + NUL;
        // name-status -z -M -C: "M\0src/app.cs\0"
        string nameStatus = "M" + NUL + "src/app.cs" + NUL;

        Route(args =>
        {
            if (args.Contains("--numstat")) return Ok(numstat);
            if (args.Contains("--name-status")) return Ok(nameStatus);
            return Ok(string.Empty);
        });

        var files = await _sut.GetChangedFilesAsync(
            "C:/repo", new DiffSpec.TwoCommits("baseSha", "targetSha"), CancellationToken.None);

        files.Count.ShouldBe(1);
        files[0].Path.ShouldBe("src/app.cs");
        files[0].Kind.ShouldBe(ChangeKind.Modified);
        files[0].AddedLines.ShouldBe(3);
        files[0].DeletedLines.ShouldBe(1);
    }

    [Fact]
    public async Task GetChangedFilesAsync_TwoCommits_PassesBothRefsToDiff()
    {
        IReadOnlyList<string>? numstatArgs = null;
        Route(args =>
        {
            if (args.Contains("--numstat")) { numstatArgs = args; return Ok(string.Empty); }
            if (args.Contains("--name-status")) return Ok(string.Empty);
            return Ok(string.Empty);
        });

        await _sut.GetChangedFilesAsync(
            "C:/repo", new DiffSpec.TwoCommits("baseSha", "targetSha"), CancellationToken.None);

        numstatArgs.ShouldNotBeNull();
        numstatArgs![0].ShouldBe("diff");
        numstatArgs.ShouldContain("-z");
        numstatArgs.ShouldContain("baseSha");
        numstatArgs.ShouldContain("targetSha");
    }

    [Fact]
    public async Task GetChangedFilesAsync_WorkingTree_AlsoRunsStatusV2ForUntracked()
    {
        string numstat = "1\t0\ttracked.cs" + NUL;
        string nameStatus = "M" + NUL + "tracked.cs" + NUL;
        // porcelain v2 untracked record: "? newfile.txt\0"
        string statusV2 = "? newfile.txt" + NUL;

        bool statusV2Called = false;
        Route(args =>
        {
            if (args.Contains("status")) { statusV2Called = true; return Ok(statusV2); }
            if (args.Contains("--numstat")) return Ok(numstat);
            if (args.Contains("--name-status")) return Ok(nameStatus);
            return Ok(string.Empty);
        });

        var files = await _sut.GetChangedFilesAsync(
            "C:/repo", new DiffSpec.WorkingTreeVsHead(), CancellationToken.None);

        statusV2Called.ShouldBeTrue();
        files.ShouldContain(f => f.Path == "newfile.txt" && f.Kind == ChangeKind.Untracked);
        files.ShouldContain(f => f.Path == "tracked.cs");
    }

    [Fact]
    public async Task GetChangedFilesAsync_RootCommit_DiffsAgainstEmptyTree()
    {
        IReadOnlyList<string>? numstatArgs = null;
        Route(args =>
        {
            // rev-list --count <sha>^ fails for a root commit (no parent).
            if (args.Contains("rev-list")) return new GitResult(128, Array.Empty<byte>(), "unknown revision");
            if (args.Contains("--numstat")) { numstatArgs = args; return Ok(string.Empty); }
            if (args.Contains("--name-status")) return Ok(string.Empty);
            return Ok(string.Empty);
        });

        await _sut.GetChangedFilesAsync(
            "C:/repo", new DiffSpec.CommitVsParent("rootSha"), CancellationToken.None);

        numstatArgs.ShouldNotBeNull();
        numstatArgs!.ShouldContain(GitConstants.EmptyTreeSha);
        numstatArgs.ShouldContain("rootSha");
    }

    [Fact]
    public async Task GetChangedFilesAsync_WhenNumstatFails_ThrowsGitCommandException()
    {
        // A failed diff (e.g. "fatal: detected dubious ownership", corrupt index) must surface
        // as an error rather than an empty file list that reads as "nothing changed".
        Route(args =>
        {
            if (args.Contains("--numstat")) return new GitResult(128, Array.Empty<byte>(), "fatal: dubious ownership");
            return Ok(string.Empty);
        });

        var ex = await Should.ThrowAsync<GitCommandException>(
            async () => await _sut.GetChangedFilesAsync(
                "C:/repo", new DiffSpec.TwoCommits("a", "b"), CancellationToken.None));

        ex.StdErr.ShouldContain("dubious ownership");
    }

    [Fact]
    public async Task GetChangedFilesAsync_WhenNameStatusFails_ThrowsGitCommandException()
    {
        Route(args =>
        {
            if (args.Contains("--numstat")) return Ok(string.Empty);
            if (args.Contains("--name-status")) return new GitResult(128, Array.Empty<byte>(), "fatal: bad object");
            return Ok(string.Empty);
        });

        await Should.ThrowAsync<GitCommandException>(
            async () => await _sut.GetChangedFilesAsync(
                "C:/repo", new DiffSpec.TwoCommits("a", "b"), CancellationToken.None));
    }

    [Fact]
    public async Task GetChangedFilesAsync_WorkingTree_StatusProbeFailure_IsToleratedNotFatal()
    {
        // The porcelain-v2 status probe only supplements untracked files; its failure must not
        // sink the whole changed-files view, which still has valid tracked-file data.
        Route(args =>
        {
            if (args.Contains("status")) return new GitResult(128, Array.Empty<byte>(), "warning: could not read");
            if (args.Contains("--numstat")) return Ok("1\t0\ttracked.cs" + NUL);
            if (args.Contains("--name-status")) return Ok("M" + NUL + "tracked.cs" + NUL);
            return Ok(string.Empty);
        });

        var files = await _sut.GetChangedFilesAsync(
            "C:/repo", new DiffSpec.WorkingTreeVsHead(), CancellationToken.None);

        files.ShouldContain(f => f.Path == "tracked.cs");
    }

    [Fact]
    public async Task GetChangedFilesAsync_NonRootCommit_DiffsAgainstCaretParent()
    {
        IReadOnlyList<string>? numstatArgs = null;
        Route(args =>
        {
            if (args.Contains("rev-list")) return Ok("1\n"); // has a parent
            if (args.Contains("--numstat")) { numstatArgs = args; return Ok(string.Empty); }
            if (args.Contains("--name-status")) return Ok(string.Empty);
            return Ok(string.Empty);
        });

        await _sut.GetChangedFilesAsync(
            "C:/repo", new DiffSpec.CommitVsParent("childSha"), CancellationToken.None);

        numstatArgs.ShouldNotBeNull();
        numstatArgs!.ShouldContain("childSha^");
        numstatArgs.ShouldContain("childSha");
    }
}
