namespace GitDelta.Core.Git;

public interface IGitProcessRunner
{
    Task<GitResult> RunAsync(string workingDirectory, IReadOnlyList<string> args, CancellationToken ct);
}
