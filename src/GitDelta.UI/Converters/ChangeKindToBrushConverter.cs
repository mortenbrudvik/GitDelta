using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using GitDelta.Core.Models;

namespace GitDelta.UI.Converters;

/// <summary>Maps a ChangeKind to a status-glyph brush (green add / red delete / orange modify / blue rename).</summary>
public sealed class ChangeKindToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = value is ChangeKind kind
            ? kind switch
            {
                ChangeKind.Added or ChangeKind.Untracked or ChangeKind.Copied => Color.FromRgb(0x3F, 0xB9, 0x50),
                ChangeKind.Deleted => Color.FromRgb(0xE5, 0x53, 0x4B),
                ChangeKind.Renamed => Color.FromRgb(0x4F, 0x9C, 0xF5),
                ChangeKind.Conflicted => Color.FromRgb(0xD2, 0x9A, 0x1F),
                _ => Color.FromRgb(0xCC, 0x8E, 0x35),
            }
            : Color.FromRgb(0x80, 0x80, 0x80);

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
