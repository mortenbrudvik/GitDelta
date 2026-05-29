namespace GitDelta.UI.Services;

/// <summary>
/// Abstraction over the OS folder-picker dialog so ViewModels that open a
/// repository remain unit-testable. The WPF implementation is registered in
/// the UI Autofac module.
/// </summary>
public interface IFolderPicker
{
    /// <summary>
    /// Shows a folder-selection dialog. Returns the chosen absolute folder
    /// path, or <c>null</c> if the user cancelled.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    string? PickFolder(string title);
}
