using System.Diagnostics;
using System.Text;
using GitDelta.Core.Git;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Git;

public sealed class GitProcessRunnerTests : IDisposable
{
    private readonly string _repoDir;

    public GitProcessRunnerTests()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), "gitdelta-runner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_repoDir);
        RunGitDirect("init -q");
        RunGitDirect("config user.email test@example.com");
        RunGitDirect("config user.name Test");
    }

    public void Dispose()
    {
        try
        {
            // .git holds read-only pack files on Windows; clear the bit before deleting.
            foreach (var file in Directory.EnumerateFiles(_repoDir, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(_repoDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; a leaked temp dir must not fail the suite.
        }
    }

    private void RunGitDirect(string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = _repoDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var process = Process.Start(psi)!;
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, $"git {args} failed: {process.StandardError.ReadToEnd()}");
    }

    [Fact]
    public async Task RunAsync_Version_ReturnsSuccessAndVersionText()
    {
        var runner = new GitProcessRunner();

        var result = await runner.RunAsync(_repoDir, ["--version"], TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        Encoding.UTF8.GetString(result.StdOut).ShouldStartWith("git version");
    }

    [Fact]
    public async Task RunAsync_StdOut_IsRawBytes_PreservingNulFraming()
    {
        File.WriteAllText(Path.Combine(_repoDir, "a.txt"), "hi\n");
        RunGitDirect("add a.txt");
        RunGitDirect("commit -q -m first");
        // Add a second commit so that 'format:' emits a NUL separator between records.
        File.WriteAllText(Path.Combine(_repoDir, "b.txt"), "world\n");
        RunGitDirect("add b.txt");
        RunGitDirect("commit -q -m second");
        var runner = new GitProcessRunner();

        // -z makes git emit NUL-separated records; raw bytes must keep the 0x00.
        var result = await runner.RunAsync(
            _repoDir, ["log", "--pretty=format:%H", "-z"], TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.StdOut.ShouldContain((byte)0x00);
    }

    [Fact]
    public async Task RunAsync_FailedCommand_ReturnsNonZeroExitAndStdErr()
    {
        var runner = new GitProcessRunner();

        var result = await runner.RunAsync(
            _repoDir, ["cat-file", "-p", "doesnotexist"], TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ExitCode.ShouldNotBe(0);
        result.StdErr.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelledToken_ThrowsOperationCanceled()
    {
        var runner = new GitProcessRunner();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await runner.RunAsync(_repoDir, ["--version"], cts.Token));
    }
}
