using GitDelta.Core.Git;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Git;

public class GitResultTests
{
    [Fact]
    public void Success_IsTrue_WhenExitCodeZero()
    {
        var result = new GitResult(0, [1, 2, 3], "");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public void Success_IsFalse_WhenExitCodeNonZero()
    {
        var result = new GitResult(128, [], "fatal: not a git repository");

        result.Success.ShouldBeFalse();
        result.StdErr.ShouldBe("fatal: not a git repository");
    }
}
