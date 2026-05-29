using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDelta.Core.Models;

namespace GitDelta.UI.ViewModels;

/// <summary>
/// Backing model for the right-hand diff view. Holds the current
/// <see cref="FileDiff"/> plus the view-affecting toggles.
/// </summary>
public partial class DiffDocumentViewModel : ObservableObject
{
    [ObservableProperty]
    private FileDiff? _fileDiff;

    [ObservableProperty]
    private DiffViewMode _viewMode = DiffViewMode.SideBySide;

    [ObservableProperty]
    private bool _showWhitespace;

    [ObservableProperty]
    private bool _wordWrap;

    [ObservableProperty]
    private int _tabSize = 4;

    [ObservableProperty]
    private string? _syntaxLanguageId;

    [ObservableProperty]
    private bool _isDarkTheme;

    [RelayCommand]
    private void ToggleViewMode()
    {
        ViewMode = ViewMode == DiffViewMode.SideBySide
            ? DiffViewMode.Unified
            : DiffViewMode.SideBySide;
    }

    [RelayCommand]
    private void ToggleWhitespace()
    {
        ShowWhitespace = !ShowWhitespace;
    }

    [RelayCommand]
    private void ToggleWordWrap()
    {
        WordWrap = !WordWrap;
    }
}
