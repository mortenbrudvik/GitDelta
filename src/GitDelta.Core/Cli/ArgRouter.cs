namespace GitDelta.Core.Cli;

/// <summary>
/// Pure, WPF-free router that maps process command-line arguments plus the
/// current working directory to a <see cref="LaunchAction"/>.
/// </summary>
/// <remarks>
/// Precedence: help flags beat version flags, which beat a repo path, which
/// beats the current working directory. This type never returns
/// <see cref="LaunchActionKind.ShowStartScreen"/>; the application bootstrap
/// downgrades an <see cref="LaunchActionKind.OpenRepoWorkingTree"/> result to
/// the start screen when the path turns out not to be a git repository.
/// </remarks>
public static class ArgRouter
{
    private static readonly string[] HelpFlags = { "--help", "-h", "-?" };
    private static readonly string[] VersionFlags = { "--version", "-v" };

    /// <summary>
    /// Decides the launch action from raw command-line <paramref name="args"/>
    /// (excluding the executable name) and the process <paramref name="cwd"/>.
    /// </summary>
    public static LaunchAction Route(IReadOnlyList<string> args, string cwd)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (MatchesAny(args[i], HelpFlags))
            {
                return new LaunchAction(LaunchActionKind.PrintHelp);
            }
        }

        for (var i = 0; i < args.Count; i++)
        {
            if (MatchesAny(args[i], VersionFlags))
            {
                return new LaunchAction(LaunchActionKind.PrintVersion);
            }
        }

        for (var i = 0; i < args.Count; i++)
        {
            if (!IsFlag(args[i]))
            {
                return new LaunchAction(LaunchActionKind.OpenRepoWorkingTree, args[i]);
            }
        }

        return new LaunchAction(LaunchActionKind.OpenRepoWorkingTree, cwd);
    }

    private static bool IsFlag(string arg) =>
        arg.StartsWith('-');

    private static bool MatchesAny(string arg, string[] flags)
    {
        foreach (var flag in flags)
        {
            if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
