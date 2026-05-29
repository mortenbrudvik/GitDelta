using Microsoft.Win32;

namespace GitDelta.UI.Services;

/// <summary>
/// WPF implementation of <see cref="IFolderPicker"/> using the .NET 10
/// <see cref="OpenFolderDialog"/> from Microsoft.Win32.
/// </summary>
public sealed class WpfFolderPicker : IFolderPicker
{
    /// <inheritdoc />
    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
