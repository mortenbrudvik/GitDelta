namespace GitDelta.Core.Git;

/// <summary>
/// Thrown when a git command that was expected to succeed returns a non-zero exit code.
/// Carries the failing operation, exit code, and stderr so the UI can show a meaningful
/// message instead of treating the failure as an empty result ("no history" / "no changes" /
/// "blank diff"), which is indistinguishable from a genuinely empty repository.
/// </summary>
public sealed class GitCommandException : Exception
{
    public GitCommandException(string operation, int exitCode, string stdErr)
        : base(BuildMessage(operation, exitCode, stdErr))
    {
        Operation = operation;
        ExitCode = exitCode;
        StdErr = stdErr;
    }

    /// <summary>The git operation that failed (e.g. "log", "diff --numstat").</summary>
    public string Operation { get; }

    /// <summary>The non-zero exit code git returned.</summary>
    public int ExitCode { get; }

    /// <summary>git's stderr output (may be empty).</summary>
    public string StdErr { get; }

    private static string BuildMessage(string operation, int exitCode, string stdErr)
    {
        string detail = string.IsNullOrWhiteSpace(stdErr)
            ? $"exit code {exitCode}"
            : stdErr.Trim();
        return $"git {operation} failed: {detail}";
    }
}
