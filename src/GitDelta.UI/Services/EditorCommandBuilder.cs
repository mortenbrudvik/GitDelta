using System.Diagnostics.CodeAnalysis;

namespace GitDelta.UI.Services;

/// <summary>
/// Pure logic for turning an external-editor command template plus a file path into a
/// <c>(FileName, Arguments)</c> pair for <see cref="System.Diagnostics.ProcessStartInfo"/>.
///
/// The template is parsed into argv FIRST (by the caller, via the OS command-line parser),
/// then the <c>{file}</c> token is substituted POST-PARSE into whichever argv element holds
/// it. This guarantees the file path stays a single argument even when it contains spaces,
/// regardless of whether the user wrote <c>{file}</c> or <c>"{file}"</c> in the template.
/// </summary>
public static class EditorCommandBuilder
{
    private const string FileToken = "{file}";

    /// <summary>
    /// Builds the launch tuple from a pre-parsed command template argv and a raw file path.
    /// </summary>
    /// <param name="templateArgv">
    /// The editor command template already split into argv (argv[0] = executable).
    /// </param>
    /// <param name="filePath">The raw absolute file path to open (may contain spaces).</param>
    /// <param name="fileName">The resolved executable to launch.</param>
    /// <param name="arguments">The assembled, properly quoted argument string.</param>
    /// <returns><see langword="true"/> if a launchable command was built; otherwise <see langword="false"/>.</returns>
    public static bool TryBuild(
        IReadOnlyList<string> templateArgv,
        string filePath,
        [NotNullWhen(true)] out string? fileName,
        out string arguments)
    {
        fileName = null;
        arguments = string.Empty;

        if (templateArgv.Count == 0 || string.IsNullOrEmpty(templateArgv[0]))
        {
            return false;
        }

        fileName = templateArgv[0];

        // Substitute {file} into each argument element (case-insensitive). The path is NOT
        // quoted here — each element is already a single argument; quoting happens at the
        // reassembly step below.
        var args = new List<string>(templateArgv.Count);
        var substituted = false;
        for (var i = 1; i < templateArgv.Count; i++)
        {
            var element = templateArgv[i];
            if (element.Contains(FileToken, StringComparison.OrdinalIgnoreCase))
            {
                element = ReplaceTokenIgnoreCase(element, FileToken, filePath);
                substituted = true;
            }

            args.Add(element);
        }

        // No {file} placeholder anywhere: append the path as a trailing argument.
        if (!substituted)
        {
            args.Add(filePath);
        }

        arguments = string.Join(' ', args.Select(QuoteIfNeeded));
        return true;
    }

    /// <summary>
    /// Wraps an argument in double quotes if it contains whitespace or a quote, escaping any
    /// embedded quotes. Already-quoted, space-free arguments are returned unchanged.
    /// </summary>
    public static string QuoteIfNeeded(string arg)
    {
        if (arg.Length == 0)
        {
            return "\"\"";
        }

        var needsQuoting = arg.AsSpan().ContainsAny(' ', '\t')
            || arg.Contains('"', StringComparison.Ordinal);

        if (!needsQuoting)
        {
            return arg;
        }

        // Escape embedded double quotes, then wrap.
        var escaped = arg.Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static string ReplaceTokenIgnoreCase(string source, string token, string replacement)
    {
        var index = source.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        var result = source;
        while (index >= 0)
        {
            result = string.Concat(result.AsSpan(0, index), replacement, result.AsSpan(index + token.Length));
            index = result.IndexOf(token, index + replacement.Length, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
