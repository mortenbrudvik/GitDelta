using System.Globalization;
using System.Text;
using GitDelta.Core.Models;

namespace GitDelta.Core.Git.Parsing;

/// <summary>
/// Parses the output of
/// 'git log --pretty=format:%H%x1f%P%x1f%an%x1f%ae%x1f%aI%x1f%cn%x1f%ce%x1f%cI%x1f%s%x1f%b -z'.
/// Records are terminated by NUL (0x00); the 10 fields within each record are
/// separated by the unit separator byte 0x1f. %P is a space-separated parent list.
/// </summary>
public static class GitLogParser
{
    private const char FieldSeparator = '\u001f';
    private const char RecordTerminator = '\0';
    private const int FieldCount = 10;

    public static IReadOnlyList<CommitInfo> Parse(byte[] stdout)
    {
        if (stdout.Length == 0)
        {
            return [];
        }

        var text = Encoding.UTF8.GetString(stdout);
        var commits = new List<CommitInfo>();

        foreach (var record in text.Split(RecordTerminator))
        {
            if (record.Length == 0)
            {
                // Trailing terminator produces a final empty segment - skip it.
                continue;
            }

            // Cap the split at FieldCount so a stray 0x1f inside the body (the last
            // field) cannot truncate the record; the body keeps any separators it holds.
            var fields = record.Split(FieldSeparator, FieldCount);
            if (fields.Length < FieldCount)
            {
                // Defensive: malformed record (e.g. missing body) - skip rather than throw.
                continue;
            }

            // Defensive: a malformed author/commit date (unexpected git config/locale) skips
            // the record rather than throwing FormatException out of history loading.
            if (!TryParseDate(fields[4], out var authorDate) || !TryParseDate(fields[7], out var commitDate))
            {
                continue;
            }

            var sha = fields[0];
            var parents = ParseParents(fields[1]);

            commits.Add(new CommitInfo(
                Sha: sha,
                ShortSha: ShortSha(sha),
                Parents: parents,
                AuthorName: fields[2],
                AuthorEmail: fields[3],
                AuthorDate: authorDate,
                CommitterName: fields[5],
                CommitterEmail: fields[6],
                CommitDate: commitDate,
                Subject: fields[8],
                Body: fields[9]));
        }

        return commits;
    }

    private static IReadOnlyList<string> ParseParents(string field)
    {
        if (field.Length == 0)
        {
            return [];
        }

        return field.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ShortSha(string sha) => sha.Length <= 7 ? sha : sha[..7];

    private static bool TryParseDate(string value, out DateTimeOffset date)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date);
}
