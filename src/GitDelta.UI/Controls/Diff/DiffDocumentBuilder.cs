using System.Text;
using GitDelta.Core.Models;

namespace GitDelta.UI.Controls.Diff;

/// <summary>
/// Builds the display document(s) and per-line classification once from a FileDiff
/// (spec §8 indexed lookup). Pure C#, no WPF — unit-tested.
/// </summary>
public static class DiffDocumentBuilder
{
    private static readonly IReadOnlyList<IntraSpan> NoSpans = Array.Empty<IntraSpan>();

    public static DiffDocumentModel Build(FileDiff diff, DiffViewMode mode) =>
        mode == DiffViewMode.Unified ? BuildUnified(diff) : BuildSideBySide(diff);

    private static DiffDocumentModel BuildUnified(FileDiff diff)
    {
        var rows = new List<DiffRow>();
        foreach (DiffHunk hunk in diff.Hunks)
        {
            foreach (DiffLine line in hunk.Lines)
            {
                DiffRowKind kind = line.Kind switch
                {
                    DiffLineKind.Added => DiffRowKind.Added,
                    DiffLineKind.Deleted => DiffRowKind.Deleted,
                    _ => DiffRowKind.Context,
                };
                rows.Add(new DiffRow(kind, line.Text, line.OldLineNumber, line.NewLineNumber, line.IntraSpans));
            }
        }

        return new DiffDocumentModel(ToSide(rows), Left: null, Right: null);
    }

    private static DiffDocumentModel BuildSideBySide(FileDiff diff)
    {
        var left = new List<DiffRow>();
        var right = new List<DiffRow>();

        foreach (DiffHunk hunk in diff.Hunks)
        {
            // Group consecutive deleted/added runs so we can pair them 1:1.
            int i = 0;
            IReadOnlyList<DiffLine> lines = hunk.Lines;
            while (i < lines.Count)
            {
                DiffLine line = lines[i];
                if (line.Kind == DiffLineKind.Context)
                {
                    left.Add(new DiffRow(DiffRowKind.Context, line.Text, line.OldLineNumber, line.NewLineNumber, line.IntraSpans));
                    right.Add(new DiffRow(DiffRowKind.Context, line.Text, line.OldLineNumber, line.NewLineNumber, line.IntraSpans));
                    i++;
                    continue;
                }

                int delStart = i;
                while (i < lines.Count && lines[i].Kind == DiffLineKind.Deleted)
                {
                    i++;
                }

                int addStart = i;
                while (i < lines.Count && lines[i].Kind == DiffLineKind.Added)
                {
                    i++;
                }

                EmitRun(lines, delStart, addStart, i, left, right);
            }
        }

        PadToEqual(left, right);
        return new DiffDocumentModel(Unified: null, ToSide(left), ToSide(right));
    }

    private static void EmitRun(
        IReadOnlyList<DiffLine> lines, int delStart, int addStart, int end,
        List<DiffRow> left, List<DiffRow> right)
    {
        int delCount = addStart - delStart;
        int addCount = end - addStart;
        int pairs = Math.Min(delCount, addCount);

        // Paired lines render on the same row so the two sides stay aligned.
        // The left cell keeps the deletion (red), the right cell is the replacement
        // (orange/Modified); intra-spans carry through on each side.
        for (int p = 0; p < pairs; p++)
        {
            DiffLine d = lines[delStart + p];
            DiffLine a = lines[addStart + p];
            left.Add(new DiffRow(DiffRowKind.Deleted, d.Text, d.OldLineNumber, null, d.IntraSpans));
            right.Add(new DiffRow(DiffRowKind.Modified, a.Text, null, a.NewLineNumber, a.IntraSpans));
        }

        // Leftover deletions: left=Deleted, right=Filler.
        for (int p = pairs; p < delCount; p++)
        {
            DiffLine d = lines[delStart + p];
            left.Add(new DiffRow(DiffRowKind.Deleted, d.Text, d.OldLineNumber, null, NoSpans));
            right.Add(Filler());
        }

        // Leftover additions: left=Filler, right=Added.
        for (int p = pairs; p < addCount; p++)
        {
            DiffLine a = lines[addStart + p];
            left.Add(Filler());
            right.Add(new DiffRow(DiffRowKind.Added, a.Text, null, a.NewLineNumber, NoSpans));
        }
    }

    private static DiffRow Filler() =>
        new(DiffRowKind.Filler, string.Empty, null, null, NoSpans);

    private static void PadToEqual(List<DiffRow> left, List<DiffRow> right)
    {
        while (left.Count < right.Count)
        {
            left.Add(Filler());
        }

        while (right.Count < left.Count)
        {
            right.Add(Filler());
        }
    }

    private static DiffDocumentSide ToSide(IReadOnlyList<DiffRow> rows)
    {
        var sb = new StringBuilder();
        for (int r = 0; r < rows.Count; r++)
        {
            if (r > 0)
            {
                sb.Append('\n');
            }

            sb.Append(rows[r].Text);
        }

        return new DiffDocumentSide(sb.ToString(), rows);
    }
}
