using System.Globalization;
using GitDelta.Core.Models;

namespace GitDelta.Core.Git.Parsing;

/// <summary>
/// Parses unified-diff text into DiffHunks. Recognizes "@@ -a,b +c,d @@ heading"
/// hunk headers (count defaults to 1 when omitted), and within each hunk classifies
/// lines by their leading character: ' ' context, '+' added, '-' deleted. The
/// "\ No newline at end of file" marker and any pre-hunk file-header lines are
/// ignored. IntraSpans are left empty (intra-line enrichment happens later).
/// </summary>
public static class UnifiedDiffParser
{
    public static IReadOnlyList<DiffHunk> Parse(string diffText)
    {
        if (string.IsNullOrEmpty(diffText))
        {
            return [];
        }

        var lines = diffText.Replace("\r\n", "\n").Split('\n');
        var hunks = new List<DiffHunk>();

        int oldLine = 0;
        int newLine = 0;
        string header = string.Empty;
        int oldStart = 0, oldCount = 0, newStart = 0, newCount = 0;
        List<DiffLine>? current = null;

        foreach (var raw in lines)
        {
            if (raw.StartsWith("@@", StringComparison.Ordinal))
            {
                var parsed = ParseHunkHeader(raw);
                if (parsed is null)
                {
                    // Malformed header (missing ranges/second '@@') — skip it without
                    // disturbing the current hunk's accumulated lines.
                    continue;
                }

                if (current is not null)
                {
                    hunks.Add(new DiffHunk(oldStart, oldCount, newStart, newCount, header, current));
                }

                (oldStart, oldCount, newStart, newCount) = parsed.Value;
                header = raw;
                oldLine = oldStart;
                newLine = newStart;
                current = [];
                continue;
            }

            if (current is null)
            {
                // Still in the pre-hunk file header (diff --git / index / --- / +++).
                continue;
            }

            if (raw.Length == 0)
            {
                // A truly empty array element (e.g. trailing split entry) — ignore.
                continue;
            }

            var marker = raw[0];
            var text = raw.Length > 1 ? raw[1..] : string.Empty;

            switch (marker)
            {
                case ' ':
                    current.Add(new DiffLine(DiffLineKind.Context, oldLine, newLine, text, []));
                    oldLine++;
                    newLine++;
                    break;

                case '-':
                    current.Add(new DiffLine(DiffLineKind.Deleted, oldLine, null, text, []));
                    oldLine++;
                    break;

                case '+':
                    current.Add(new DiffLine(DiffLineKind.Added, null, newLine, text, []));
                    newLine++;
                    break;

                case '\\':
                    // "\ No newline at end of file" — metadata, not a content line.
                    break;

                default:
                    // Unknown leading char outside a hunk body — ignore defensively.
                    break;
            }
        }

        if (current is not null)
        {
            hunks.Add(new DiffHunk(oldStart, oldCount, newStart, newCount, header, current));
        }

        return hunks;
    }

    private static (int OldStart, int OldCount, int NewStart, int NewCount)? ParseHunkHeader(string header)
    {
        // Format: "@@ -oldStart[,oldCount] +newStart[,newCount] @@[ heading]"
        var firstAt = header.IndexOf("@@", StringComparison.Ordinal);
        var secondAt = header.IndexOf("@@", firstAt + 2, StringComparison.Ordinal);
        if (secondAt < 0)
        {
            // Malformed: no closing "@@" — cannot locate the range section.
            return null;
        }

        var ranges = header[(firstAt + 2)..secondAt].Trim();

        var parts = ranges.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            // Malformed: missing the '-' or '+' range token.
            return null;
        }

        var (oldStart, oldCount) = ParseRange(parts[0]); // leading '-'
        var (newStart, newCount) = ParseRange(parts[1]); // leading '+'
        return (oldStart, oldCount, newStart, newCount);
    }

    private static (int Start, int Count) ParseRange(string token)
    {
        // token is like "-12,5" or "+3" — strip the sign, split on ','.
        var body = token[1..];
        var comma = body.IndexOf(',');
        if (comma < 0)
        {
            return (int.Parse(body, CultureInfo.InvariantCulture), 1);
        }

        var start = int.Parse(body[..comma], CultureInfo.InvariantCulture);
        var count = int.Parse(body[(comma + 1)..], CultureInfo.InvariantCulture);
        return (start, count);
    }
}
