using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace GitDelta.UI.Controls.Diff;

/// <summary>
/// A narrow gutter margin that draws a solid change-bar (green/red/orange)
/// for each added/deleted/modified line, driven by the per-line classification.
/// </summary>
public sealed class ChangeBarMargin : AbstractMargin
{
    private const double BarWidth = 4.0;
    private DiffDocumentSide? _side;

    public void SetSide(DiffDocumentSide? side)
    {
        _side = side;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(BarWidth, 0);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        TextView? textView = TextView;
        DiffDocumentSide? side = _side;
        if (textView is null || side is null || !textView.VisualLinesValid)
        {
            return;
        }

        foreach (VisualLine vl in textView.VisualLines)
        {
            int rowIndex = vl.FirstDocumentLine.LineNumber - 1;
            if (rowIndex < 0 || rowIndex >= side.Rows.Count)
            {
                continue;
            }

            Brush? brush = side.Rows[rowIndex].Kind switch
            {
                DiffRowKind.Added => DiffBrushes.AddedBar,
                DiffRowKind.Deleted => DiffBrushes.DeletedBar,
                DiffRowKind.Modified => DiffBrushes.ModifiedBar,
                _ => null,
            };

            if (brush is null)
            {
                continue;
            }

            double top = vl.VisualTop - textView.VerticalOffset;
            double height = vl.Height;
            drawingContext.DrawRectangle(brush, null, new Rect(0, top, BarWidth, height));
        }
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView is not null)
        {
            oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
        }

        base.OnTextViewChanged(oldTextView, newTextView);

        if (newTextView is not null)
        {
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
        }

        InvalidateVisual();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();
}
