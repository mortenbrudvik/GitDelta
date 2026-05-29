namespace GitDelta.Core.Git;

public sealed record GitResult(int ExitCode, byte[] StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
}
