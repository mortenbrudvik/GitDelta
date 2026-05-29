namespace GitDelta.UI.Cli;

/// <summary>Renders --help / --version text to stdout.</summary>
internal static class ConsoleOutput
{
    /// <summary>Writes the multi-line help text to <see cref="Console.Out"/>.</summary>
    public static void WriteHelp() => Console.WriteLine(CliHelpText.Help);

    /// <summary>Writes the version line to <see cref="Console.Out"/>.</summary>
    public static void WriteVersion() => Console.WriteLine(CliHelpText.Version);
}
