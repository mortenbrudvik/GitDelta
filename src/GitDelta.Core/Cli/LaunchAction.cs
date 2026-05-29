namespace GitDelta.Core.Cli;

/// <summary>
/// The high-level action the application bootstrap should take, decided purely
/// from the process command-line arguments and the current working directory.
/// </summary>
public enum LaunchActionKind
{
    /// <summary>Show the brand/Open-folder start screen (no usable repo context).</summary>
    ShowStartScreen,

    /// <summary>Open a repository at <see cref="LaunchAction.RepoPath"/> in working-tree-vs-HEAD mode.</summary>
    OpenRepoWorkingTree,

    /// <summary>Write CLI help text to the parent console and exit.</summary>
    PrintHelp,

    /// <summary>Write the version string to the parent console and exit.</summary>
    PrintVersion,
}

/// <summary>
/// The routing decision produced by <see cref="ArgRouter.Route"/>.
/// <see cref="RepoPath"/> is non-null only when <see cref="Kind"/> is
/// <see cref="LaunchActionKind.OpenRepoWorkingTree"/>.
/// </summary>
public sealed record LaunchAction(LaunchActionKind Kind, string? RepoPath = null);
