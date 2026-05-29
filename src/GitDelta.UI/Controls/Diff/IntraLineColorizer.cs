using GitDelta.Core.Models;
using ICSharpCode.AvalonEdit.Rendering;

namespace GitDelta.UI.Controls.Diff;

/// <summary>
/// Applies intra-line (word-level) background tints from IntraSpan ranges.
/// Added after the syntax transformer so its background layers over syntax
/// foreground colors (spec §8). Per-visible-line via AvalonEdit virtualization.
/// </summary>
public sealed class IntraLineColorizer : DocumentColorizingTransformer
{
    private DiffDocumentSide? _side;

    public void SetSide(DiffDocumentSide? side)
    {
        _side = side;
    }

    protected override void ColorizeLine(ICSharpCode.AvalonEdit.Document.DocumentLine line)
    {
        DiffDocumentSide? side = _side;
        if (side is null)
        {
            return;
        }

        int rowIndex = line.LineNumber - 1;
        if (rowIndex < 0 || rowIndex >= side.Rows.Count)
        {
            return;
        }

        DiffRow row = side.Rows[rowIndex];
        if (row.IntraSpans.Count == 0)
        {
            return;
        }

        int lineStart = line.Offset;
        int lineLength = line.Length;

        foreach (IntraSpan span in row.IntraSpans)
        {
            int spanStart = span.Start;
            int spanEnd = span.Start + span.Length;

            // Clamp to the actual line bounds (defensive against off-by-one in source data).
            if (spanStart < 0)
            {
                spanStart = 0;
            }

            if (spanEnd > lineLength)
            {
                spanEnd = lineLength;
            }

            if (spanEnd <= spanStart)
            {
                continue;
            }

            System.Windows.Media.Brush tint = span.Kind == IntraSpanKind.Added
                ? DiffBrushes.AddedSpan
                : DiffBrushes.DeletedSpan;

            ChangeLinePart(
                lineStart + spanStart,
                lineStart + spanEnd,
                element => element.TextRunProperties.SetBackgroundBrush(tint));
        }
    }
}
