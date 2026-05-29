using System.Text;
using GitDelta.Core.Diff;
using GitDelta.Core.Git;
using GitDelta.Core.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Git;

public class CliGitReaderFileDiffTests
{
    private const string NUL = "\0";

    private readonly IGitProcessRunner _runner = Substitute.For<IGitProcessRunner>();
    private readonly CliGitReader _sut;

    public CliGitReaderFileDiffTests()
    {
        _sut = new CliGitReader(_runner, new DiffPlexIntraLineDiffer());
    }

    private void Route(Func<IReadOnlyList<string>, GitResult> selector) =>
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
               .Returns(ci => Task.FromResult(selector(ci.ArgAt<IReadOnlyList<string>>(1))));

    private static GitResult Ok(string s) => new(0, Encoding.UTF8.GetBytes(s), string.Empty);

    private const string TextualDiff =
        "diff --git a/app.cs b/app.cs\n" +
        "index 1111111..2222222 100644\n" +
        "--- a/app.cs\n" +
        "+++ b/app.cs\n" +
        "@@ -1,3 +1,3 @@\n" +
        " context line\n" +
        "-var quick = 1;\n" +
        "+var slow = 1;\n" +
        " trailing line\n";

    private void RouteTextual()
    {
        string numstat = "1\t1\tapp.cs" + NUL;
        string nameStatus = "M" + NUL + "app.cs" + NUL;
        Route(args =>
        {
            if (args.Contains("rev-list")) return Ok("1\n");
            if (args.Contains("--numstat")) return Ok(numstat);
            if (args.Contains("--name-status")) return Ok(nameStatus);
            return Ok(TextualDiff); // the per-file 'git diff -U.. -- path'
        });
    }

    [Fact]
    public async Task GetFileDiffAsync_PassesContextRefsAndPathSeparator()
    {
        IReadOnlyList<string>? diffArgs = null;
        string numstat = "1\t1\tapp.cs" + NUL;
        string nameStatus = "M" + NUL + "app.cs" + NUL;
        Route(args =>
        {
            if (args.Contains("rev-list")) return Ok("1\n");
            if (args.Contains("--numstat")) return Ok(numstat);
            if (args.Contains("--name-status")) return Ok(nameStatus);
            diffArgs = args;
            return Ok(TextualDiff);
        });

        await _sut.GetFileDiffAsync(
            "C:/repo", new DiffSpec.TwoCommits("baseSha", "targetSha"), "app.cs", contextLines: 5, CancellationToken.None);

        diffArgs.ShouldNotBeNull();
        diffArgs![0].ShouldBe("diff");
        diffArgs.ShouldContain("-U5");
        diffArgs.ShouldContain("baseSha");
        diffArgs.ShouldContain("targetSha");
        diffArgs.ShouldContain("--");
        diffArgs[^1].ShouldBe("app.cs");
    }

    [Fact]
    public async Task GetFileDiffAsync_ParsesHunksAndEnrichesIntraLine()
    {
        RouteTextual();

        var diff = await _sut.GetFileDiffAsync(
            "C:/repo", new DiffSpec.TwoCommits("baseSha", "targetSha"), "app.cs", contextLines: 3, CancellationToken.None);

        diff.IsBinary.ShouldBeFalse();
        diff.IsTruncated.ShouldBeFalse();
        diff.Hunks.Count.ShouldBe(1);

        var deleted = diff.Hunks[0].Lines.Single(l => l.Kind == DiffLineKind.Deleted);
        var added = diff.Hunks[0].Lines.Single(l => l.Kind == DiffLineKind.Added);

        // Intra-line enrichment paired the single delete/add and isolated quick -> slow.
        deleted.IntraSpans.ShouldNotBeEmpty();
        added.IntraSpans.ShouldNotBeEmpty();
        deleted.Text.Substring(deleted.IntraSpans[0].Start, deleted.IntraSpans[0].Length).ShouldBe("quick");
    }

    [Fact]
    public async Task GetFileDiffAsync_BinaryFile_SetsIsBinaryAndNoHunks()
    {
        string numstat = "-\t-\timg.png" + NUL; // binary marker
        string nameStatus = "M" + NUL + "img.png" + NUL;
        Route(args =>
        {
            if (args.Contains("rev-list")) return Ok("1\n");
            if (args.Contains("--numstat")) return Ok(numstat);
            if (args.Contains("--name-status")) return Ok(nameStatus);
            return Ok("diff --git a/img.png b/img.png\nBinary files a/img.png and b/img.png differ\n");
        });

        var diff = await _sut.GetFileDiffAsync(
            "C:/repo", new DiffSpec.TwoCommits("baseSha", "targetSha"), "img.png", contextLines: 3, CancellationToken.None);

        diff.IsBinary.ShouldBeTrue();
        diff.Hunks.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFileDiffAsync_OverLineThreshold_SetsIsTruncatedAndDropsHunks()
    {
        // Build a unified diff whose body exceeds the truncation threshold.
        var sb = new StringBuilder();
        sb.Append("diff --git a/big.cs b/big.cs\n--- a/big.cs\n+++ b/big.cs\n");
        int lines = CliGitReader.LargeFileHunkLineThreshold + 10;
        sb.Append("@@ -1," + lines + " +1," + lines + " @@\n");
        for (int i = 0; i < lines; i++)
        {
            sb.Append("+added line " + i + "\n");
        }

        string numstat = lines + "\t0\tbig.cs" + NUL;
        string nameStatus = "M" + NUL + "big.cs" + NUL;
        Route(args =>
        {
            if (args.Contains("rev-list")) return Ok("1\n");
            if (args.Contains("--numstat")) return Ok(numstat);
            if (args.Contains("--name-status")) return Ok(nameStatus);
            return Ok(sb.ToString());
        });

        var diff = await _sut.GetFileDiffAsync(
            "C:/repo", new DiffSpec.TwoCommits("baseSha", "targetSha"), "big.cs", contextLines: 3, CancellationToken.None);

        diff.IsTruncated.ShouldBeTrue();
        diff.Hunks.ShouldBeEmpty();
        diff.IsBinary.ShouldBeFalse();
    }

    [Fact]
    public async Task GetFileDiffAsync_CarriesChangedFileMetadata()
    {
        RouteTextual();

        var diff = await _sut.GetFileDiffAsync(
            "C:/repo", new DiffSpec.TwoCommits("baseSha", "targetSha"), "app.cs", contextLines: 3, CancellationToken.None);

        diff.File.Path.ShouldBe("app.cs");
        diff.File.Kind.ShouldBe(ChangeKind.Modified);
    }
}
