using GitDelta.Core.Git;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Git;

public class DiffSpecArgsTests
{
    [Fact]
    public void EmptyTreeSha_IsTheWellKnownGitConstant()
    {
        GitConstants.EmptyTreeSha.ShouldBe("4b825dc642cb6eb9a060e54bf8d69288fbee4904");
    }

    [Fact]
    public void ToDiffArgs_WorkingTreeVsHead_ReturnsHead()
    {
        var args = DiffSpecArgs.ToDiffArgs(new DiffSpec.WorkingTreeVsHead(), isRootCommit: false);

        args.ShouldBe(["HEAD"]);
    }

    [Fact]
    public void ToDiffArgs_CommitVsParent_NonRoot_ReturnsParentCaretAndSha()
    {
        var args = DiffSpecArgs.ToDiffArgs(new DiffSpec.CommitVsParent("deadbeef"), isRootCommit: false);

        args.ShouldBe(["deadbeef^", "deadbeef"]);
    }

    [Fact]
    public void ToDiffArgs_CommitVsParent_Root_ReturnsEmptyTreeAndSha()
    {
        var args = DiffSpecArgs.ToDiffArgs(new DiffSpec.CommitVsParent("rootsha"), isRootCommit: true);

        args.ShouldBe(["4b825dc642cb6eb9a060e54bf8d69288fbee4904", "rootsha"]);
    }

    [Fact]
    public void ToDiffArgs_TwoCommits_ReturnsBaseThenTarget()
    {
        var args = DiffSpecArgs.ToDiffArgs(new DiffSpec.TwoCommits("base111", "target222"), isRootCommit: false);

        args.ShouldBe(["base111", "target222"]);
    }
}
