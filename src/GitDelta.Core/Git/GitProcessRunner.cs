using System.Diagnostics;
using System.Text;

namespace GitDelta.Core.Git;

/// <summary>
/// Runs the git CLI as a child process. stdout is captured as raw bytes (so NUL/0x1f
/// framing and non-UTF-8 paths survive), stderr is decoded as UTF-8. Every invocation
/// is prefixed with read-only/quoting flags. Cancellation kills the process tree.
/// </summary>
public sealed class GitProcessRunner : IGitProcessRunner
{
    public async Task<GitResult> RunAsync(string workingDirectory, IReadOnlyList<string> args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var psi = new ProcessStartInfo("git")
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
        process.Start();

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
