using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDelta.UI.Services;

namespace GitDelta.UI.ViewModels;

/// <summary>
/// The start screen shown when GitDelta launches without a repo context.
/// Lets the user pick a folder, and surfaces a git-not-installed warning.
/// </summary>
public partial class StartViewModel : ObservableObject
{
    private readonly IFolderPicker _folderPicker;

    public StartViewModel(IFolderPicker folderPicker)
    {
        _folderPicker = folderPicker;
    }

    [ObservableProperty]
    private bool _gitMissing;

    public string GitMissingMessage =>
        "Git was not found on your PATH. GitDelta needs Git for Windows " +
        "(version 2.30 or newer). Install it, then restart GitDelta.";

    /// <summary>Raised with the chosen absolute folder path.</summary>
    public event Action<string>? RepositorySelected;

    [RelayCommand]
    private Task OpenFolderAsync()
    {
        string? folder = _folderPicker.PickFolder("Open a Git repository");
        if (!string.IsNullOrEmpty(folder))
        {
            RepositorySelected?.Invoke(folder);
        }

        return Task.CompletedTask;
    }
}
