using System.Windows;
using System.Windows.Media;

namespace GitDelta.UI.Controls.Diff;

/// <summary>
/// Resolves diff render brushes from merged application resources by key.
/// Resolving live (not caching) lets theme swaps recolor the diff (spec §8).
/// Falls back to hard-coded brushes if a key is missing (e.g. design-time).
/// </summary>
public static class DiffBrushes
{
    public static Brush AddedLine => Resolve("DiffAddedLineBrush", 0x33, 0x22, 0xC5, 0x5E);
    public static Brush DeletedLine => Resolve("DiffDeletedLineBrush", 0x33, 0xEF, 0x44, 0x44);
    public static Brush ModifiedLine => Resolve("DiffModifiedLineBrush", 0x33, 0xF5, 0x9E, 0x0B);
    public static Brush FillerLine => Resolve("DiffFillerLineBrush", 0x14, 0x80, 0x80, 0x80);

    public static Brush AddedSpan => Resolve("DiffAddedSpanBrush", 0x55, 0x22, 0xC5, 0x5E);
    public static Brush DeletedSpan => Resolve("DiffDeletedSpanBrush", 0x55, 0xEF, 0x44, 0x44);

    public static Brush AddedBar => Resolve("DiffAddedBarBrush", 0xFF, 0x22, 0xC5, 0x5E);
    public static Brush DeletedBar => Resolve("DiffDeletedBarBrush", 0xFF, 0xEF, 0x44, 0x44);
    public static Brush ModifiedBar => Resolve("DiffModifiedBarBrush", 0xFF, 0xF5, 0x9E, 0x0B);

    private static Brush Resolve(string key, byte a, byte r, byte g, byte b)
    {
        if (Application.Current?.TryFindResource(key) is Brush brush)
        {
            return brush;
        }

        var fallback = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        fallback.Freeze();
        return fallback;
    }
}
