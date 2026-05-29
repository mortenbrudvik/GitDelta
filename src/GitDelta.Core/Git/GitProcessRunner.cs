using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace GitDelta.Core.Git;

/// <summary>
/// Runs the git CLI as a child process. stdout is captured as raw bytes (so NUL/0x1f
/// framing and non-UTF-8 paths survive), stderr is decoded as UTF-8. Every invocation
/// is prefixed with read-only/quoting flags. Cancellation kills the process tree.
/// git is resolved from PATH; availability/version is validated separately by
/// <see cref="CliGitReader.CheckGitAsync"/>.
/// </summary>
public sealed class GitProcessRunner : IGitProcessRunner
{
    /// <summary>
    /// Exit code reported when the git process could not even be started (e.g. git is
    /// not on PATH). Distinct from any real git exit code; only <see cref="GitResult.Success"/>
    /// (== 0) matters to callers, so any non-zero sentinel signals failure.
    /// </summary>
    private const int ProcessNotStartedExitCode = -1;

    private readonly string _gitExecutable;

    /// <param name="gitExecutable">
    /// Executable to launch; defaults to "git" (resolved from PATH). Overridable as a test seam.
    /// </param>
    public GitProcessRunner(string gitExecutable = "git")
    {
        _gitExecutable = gitExecutable;
    }

    public async Task<GitResult> RunAsync(string workingDirectory, IReadOnlyList<string> args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var psi = new ProcessStartInfo(_gitExecutable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8
        };

        // Read-only safety + stable path quoting, prepended before the caller's args.
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("core.quotepath=false");
        psi.ArgumentList.Add("--no-optional-locks");
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            // git could not be launched (most commonly: not on PATH). Surface this as a
            // failed result rather than letting the Win32Exception escape — CheckGitAsync
            // turns it into "git not installed" and other callers can report the failure.
            return new GitResult(ProcessNotStartedExitCode, Array.Empty<byte>(), ex.Message);
        }

        using var stdOutBuffer = new MemoryStream();

        // Drain stdout as raw bytes and stderr as UTF-8 text concurrently to avoid
        // pipe-buffer deadlock on large outputs.
        var stdOutTask = process.StandardOutput.BaseStream.CopyToAsync(stdOutBuffer, ct);
        var stdErrTask = process.StandardError.ReadToEndAsync(ct);

        try
        {
            await Task.WhenAll(stdOutTask, stdErrTask).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        // stdErrTask already completed in the Task.WhenAll above; await it (no blocking).
        var stdErr = await stdErrTask.ConfigureAwait(false);
        return new GitResult(process.ExitCode, stdOutBuffer.ToArray(), stdErr);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Process may have already exited between the check and the kill.
        }
    }
}
