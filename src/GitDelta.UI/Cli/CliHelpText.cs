using System.Reflection;

namespace GitDelta.UI.Cli;

/// <summary>
/// Pure helper that produces the --help and --version text strings.
/// Having these as plain string properties makes them unit-testable without
/// any console or P/Invoke involvement.
/// </summary>
internal static class CliHelpText
{
    /// <summary>
    /// The multi-line help text shown by --help / -h / -?.
    /// </summary>
    public static string Help =>
        """
        GitDelta - a read-only Git diff and commit viewer.

        Usage:
          gitdelta                 Open the repository in the current directory.
          gitdelta <path>          Open the repository at <path>.
          gitdelta --help, -h, -?  Show this help.
          gitdelta --version, -v   Show the version.

        With no repository context, GitDelta opens its start screen.
        """;

    /// <summary>
    /// The single-line version string shown by --version / -v.
    /// Reads the assembly informational version and strips any build-metadata suffix.
    /// </summary>
    public static string Version
    {
        get
        {
            var raw = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? "0.0.0";

            // Trim any build metadata suffix (e.g. "1.2.3+abcdef").
            var plus = raw.IndexOf('+');
            if (plus >= 0)
            {
                raw = raw[..plus];
            }

            return $"gitdelta {raw}";
        }
    }
}
