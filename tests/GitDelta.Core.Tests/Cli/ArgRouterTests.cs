using GitDelta.Core.Cli;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Cli;

public class ArgRouterTests
{
    private const string Cwd = @"C:\work\some-repo";

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    [InlineData("--HELP")] // recognized flags are case-insensitive
    public void Route_HelpFlag_ReturnsPrintHelp(string flag)
    {
        var action = ArgRouter.Route(new[] { flag }, Cwd);

        action.Kind.ShouldBe(LaunchActionKind.PrintHelp);
        action.RepoPath.ShouldBeNull();
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    [InlineData("--VERSION")] // recognized flags are case-insensitive
    public void Route_VersionFlag_ReturnsPrintVersion(string flag)
    {
        var action = ArgRouter.Route(new[] { flag }, Cwd);

        action.Kind.ShouldBe(LaunchActionKind.PrintVersion);
        action.RepoPath.ShouldBeNull();
    }

    [Fact]
    public void Route_PathArg_ReturnsOpenRepoWorkingTreeWithThatPath()
    {
        var action = ArgRouter.Route(new[] { @"D:\projects\other-repo" }, Cwd);

        action.Kind.ShouldBe(LaunchActionKind.OpenRepoWorkingTree);
        action.RepoPath.ShouldBe(@"D:\projects\other-repo");
    }

    [Fact]
    public void Route_NoArgs_ReturnsOpenRepoWorkingTreeWithCwd()
    {
        var action = ArgRouter.Route(Array.Empty<string>(), Cwd);

        action.Kind.ShouldBe(LaunchActionKind.OpenRepoWorkingTree);
        action.RepoPath.ShouldBe(Cwd);
    }

    [Fact]
    public void Route_HelpFlagMixedWithPath_HelpWins()
    {
        var action = ArgRouter.Route(new[] { @"D:\projects\other-repo", "--help" }, Cwd);

        action.Kind.ShouldBe(LaunchActionKind.PrintHelp);
        action.RepoPath.ShouldBeNull();
    }

    [Fact]
    public void Route_VersionFlagMixedWithPath_VersionWins()
    {
        var action = ArgRouter.Route(new[] { @"D:\projects\other-repo", "-v" }, Cwd);

        action.Kind.ShouldBe(LaunchActionKind.PrintVersion);
        action.RepoPath.ShouldBeNull();
    }

    [Fact]
    public void Route_HelpAndVersionTogether_HelpTakesPrecedence()
    {
        var action = ArgRouter.Route(new[] { "--version", "--help" }, Cwd);

        action.Kind.ShouldBe(LaunchActionKind.PrintHelp);
        action.RepoPath.ShouldBeNull();
    }

    [Fact]
    public void Route_FlagBeforePath_UsesFirstNonFlagArgAsPath()
    {
        var action = ArgRouter.Route(new[] { "--unknown", @"E:\repo" }, Cwd);

        action.Kind.ShouldBe(LaunchActionKind.OpenRepoWorkingTree);
        action.RepoPath.ShouldBe(@"E:\repo");
    }

    [Fact]
    public void Route_OnlyUnrecognizedFlag_FallsBackToCwd()
    {
        var action = ArgRouter.Route(new[] { "--unknown" }, Cwd);

        action.Kind.ShouldBe(LaunchActionKind.OpenRepoWorkingTree);
        action.RepoPath.ShouldBe(Cwd);
    }

    [Fact]
    public void Route_TwoPathArgs_UsesFirstNonFlagArg()
    {
        var action = ArgRouter.Route(new[] { @"C:\first", @"C:\second" }, Cwd);

        action.Kind.ShouldBe(LaunchActionKind.OpenRepoWorkingTree);
        action.RepoPath.ShouldBe(@"C:\first");
    }
}
