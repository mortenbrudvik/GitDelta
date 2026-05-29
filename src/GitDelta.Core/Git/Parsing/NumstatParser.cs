using System.Globalization;
using System.Text;

namespace GitDelta.Core.Git.Parsing;

/// <summary>
/// Parses 'git diff --numstat -z' output. Each entry begins with
/// "&lt;added&gt;\t&lt;deleted&gt;\t". For a normal change the path follows and is
/// NUL-terminated. For a rename/copy the counts are followed immediately by a NUL,
/// then "&lt;oldpath&gt;\0&lt;newpath&gt;\0". Binary files use "-" for both counts.
/// </summary>
public static class NumstatParser
{
    private const char Nul = '\0';

    public static IReadOnlyList<NumstatEntry> Parse(byte[] stdout)
    {
        if (stdout.Length == 0)
        {
            return [];
        }

        var text = Encoding.UTF8.GetString(stdout);
        var entries = new List<NumstatEntry>();
        var index = 0;

        while (index < text.Length)
        {
            // Each entry's count header is "added \t deleted \t" before the first NUL boundary.
            // Find the end of "added\tdeleted\t": it is the position just past the second tab.
            var firstTab = text.IndexOf('\t', index);
            if (firstTab < 0)
            {
                break;
            }

            var secondTab = text.IndexOf('\t', firstTab + 1);
            if (secondTab < 0)
            {
                break;
            }

            var addedToken = text[index..firstTab];
            var deletedToken = text[(firstTab + 1)..secondTab];
            var isBinary = addedToken == "-" && deletedToken == "-";
            int? added = isBinary ? null : ParseCount(addedToken);
            int? deleted = isBinary ? null : ParseCount(deletedToken);

            // Position right after the second tab. A rename has a NUL here first.
            var afterCounts = secondTab + 1;

            if (afterCounts < text.Length && text[afterCounts] == Nul)
            {
                // Rename/copy: \0 oldpath \0 newpath \0
                var oldStart = afterCounts + 1;
                var oldEnd = text.IndexOf(Nul, oldStart);
                if (oldEnd < 0)
                {
                    break;
                }

                var newStart = oldEnd + 1;
                var newEnd = text.IndexOf(Nul, newStart);
                if (newEnd < 0)
                {
                    break;
                }

                var oldPath = text[oldStart..oldEnd];
                var newPath = text[newStart..newEnd];
                entries.Add(new NumstatEntry(newPath, oldPath, added, deleted, isBinary));
                index = newEnd + 1;
            }
            else
            {
                // Normal: path \0
                var pathEnd = text.IndexOf(Nul, afterCounts);
                if (pathEnd < 0)
                {
                    break;
                }

                var path = text[afterCounts..pathEnd];
                entries.Add(new NumstatEntry(path, null, added, deleted, isBinary));
                index = pathEnd + 1;
            }
        }

        return entries;
    }

    private static int? ParseCount(string token)
        => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null; // malformed count: report as unknown rather than throwing.
}
