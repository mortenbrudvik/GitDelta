using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Rendering;

namespace GitDelta.UI.Controls.Diff;

/// <summary>
/// Paints full-line add/delete/modify/filler backgrounds behind the text,
/// driven by the precomputed per-line classification. Operates only on visible
/// lines (AvalonEdit virtualization).
/// </summary>
public sealed class DiffLineBackgroundRenderer : IBackgroundRenderer
{
    private DiffDocumentSide? _side;

    public KnownLayer Layer => KnownLayer.Background;

    public void SetSide(DiffDocumentSide? side)
    {
        _side = side;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        DiffDocumentSide? side = _side;
        if (side is null || !textView.VisualLinesValid)
        {
            return;
        }

        textView.EnsureVisualLines();

        foreach (VisualLine vl in textView.VisualLines)
        {
            int docLine = vl.FirstDocumentLine.LineNumber; // 1-based
            int rowIndex = docLine - 1;
            if (rowIndex < 0 || rowIndex >= side.Rows.Count)
            {
                continue;
            }

            Brush? brush = side.Rows[rowIndex].Kind switch
            {
                DiffRowKind.Added => DiffBrushes.AddedLine,
                DiffRowKind.Deleted => DiffBrushes.DeletedLine,
                DiffRowKind.Modified => DiffBrushes.ModifiedLine,
                DiffRowKind.Filler => DiffBrushes.FillerLine,
                _ => null,
            };

            if (brush is null)
            {
                continue;
            }

            var geoBuilder = new BackgroundGeometryBuilder { AlignToWholePixels = true };
            foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(
                textView, vl.FirstDocumentLine, extendToFullWidthAtLineEnd: true))
            {
                // Fill the full editor width so the line background reads as a band.
                geoBuilder.AddRectangle(0, rect.Top, textView.ActualWidth, rect.Bottom);
            }

            Geometry? geometry = geoBuilder.CreateGeometry();
            if (geometry is not null)
            {
                drawingContext.DrawGeometry(brush, null, geometry);
            }
        }
    }
}
