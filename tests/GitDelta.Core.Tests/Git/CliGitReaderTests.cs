using System.Text;
using GitDelta.Core.Diff;
using GitDelta.Core.Git;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Git;

public class CliGitReaderTests
{
    private readonly IGitProcessRunner _runner = Substitute.For<IGitProcessRunner>();
    private readonly CliGitReader _sut;

    public CliGitReaderTests()
    {
        _sut = new CliGitReader(_runner, new DiffPlexIntraLineDiffer());
    }

    private static GitResult Ok(string stdout) =>
        new(0, Encoding.UTF8.GetBytes(stdout), string.Empty);

    private static GitResult Ok(byte[] stdout) =>
        new(0, stdout, string.Empty);

    private static GitResult Fail(string stderr) =>
        new(128, Array.Empty<byte>(), stderr);

    private void SetupRun(GitResult result) =>
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(result));

    [Fact]
    public async Task CheckGitAsync_ModernVersion_IsInstalledAndMeetsMinimum()
    {
        SetupRun(Ok("git version 2.43.0.windows.1\n"));

        var availability = await _sut.CheckGitAsync(CancellationToken.None);

        availability.IsInstalled.ShouldBeTrue();
        availability.Version.ShouldBe("2.43.0.windows.1");
        availability.MeetsMinimum.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckGitAsync_ExactlyMinimumVersion_MeetsMinimum()
    {
        SetupRun(Ok("git version 2.30.0\n"));

        var availability = await _sut.CheckGitAsync(CancellationToken.None);

        availability.MeetsMinimum.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckGitAsync_OldVersion_DoesNotMeetMinimum()
    {
        SetupRun(Ok("git version 2.20.1\n"));

        var availability = await _sut.CheckGitAsync(CancellationToken.None);

        availability.IsInstalled.ShouldBeTrue();
        availability.MeetsMinimum.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckGitAsync_GitNotFound_ReportsNotInstalled()
    {
        SetupRun(Fail("'git' is not recognized"));

        var availability = await _sut.CheckGitAsync(CancellationToken.None);

        availability.IsInstalled.ShouldBeFalse();
        availability.Version.ShouldBeNull();
        availability.MeetsMinimum.ShouldBeFalse();
    }

    [Fact]
    public async Task FindRepositoryRootAsync_InsideRepo_ReturnsTrimmedToplevel()
    {
        SetupRun(Ok("C:/code/projects/GitDelta\n"));

        var root = await _sut.FindRepositoryRootAsync("C:/code/projects/GitDelta/src", CancellationToken.None);

        root.ShouldBe("C:/code/projects/GitDelta");
    }

    [Fact]
    public async Task FindRepositoryRootAsync_NotARepo_ReturnsNull()
    {
        SetupRun(new GitResult(128, Array.Empty<byte>(), "fatal: not a git repository"));

        var root = await _sut.FindRepositoryRootAsync("C:/temp", CancellationToken.None);

        root.ShouldBeNull();
    }
}
