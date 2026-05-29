using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDelta.Core.Models;

namespace GitDelta.UI.ViewModels;

/// <summary>
/// Bindable mirror of <see cref="AppSettings"/> for the settings dialog.
/// <see cref="Result"/> exposes the (possibly edited) settings; the shell
/// inspects <see cref="SaveRequested"/> to decide whether to persist.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _original;

    public SettingsViewModel(AppSettings current)
    {
        _original = current;
        _theme = current.Theme;
        _defaultDiffView = current.DefaultDiffView;
        _contextLines = current.ContextLines;
        _tabSize = current.TabSize;
        _syntaxHighlighting = current.SyntaxHighlighting;
        _externalEditorCommand = current.ExternalEditorCommand;
        Result = current;
    }

    [ObservableProperty]
    private AppTheme _theme;

    [ObservableProperty]
    private DiffViewMode _defaultDiffView;

    [ObservableProperty]
    private int _contextLines;

    [ObservableProperty]
    private int _tabSize;

    [ObservableProperty]
    private bool _syntaxHighlighting;

    [ObservableProperty]
    private string? _externalEditorCommand;

    /// <summary>The settings to apply once the dialog closes.</summary>
    public AppSettings Result { get; private set; }

    /// <summary>True after <see cref="SaveCommand"/> has executed.</summary>
    public bool SaveRequested { get; private set; }

    [RelayCommand]
    private void Save()
    {
        Result = _original with
        {
            Theme = Theme,
            DefaultDiffView = DefaultDiffView,
            ContextLines = ContextLines,
            TabSize = TabSize,
            SyntaxHighlighting = SyntaxHighlighting,
            ExternalEditorCommand = ExternalEditorCommand
        };
        SaveRequested = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        Result = _original;
        SaveRequested = false;
    }
}
