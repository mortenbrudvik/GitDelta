using System.Windows;
using System.Windows.Controls;
using GitDelta.Core.Models;
using ICSharpCode.AvalonEdit;

namespace GitDelta.UI.Controls;

/// <summary>
/// Read-only AvalonEdit-based diff renderer. SideBySide uses two editors with
/// filler/imaginary lines and mirrored scroll; Unified uses a single editor.
/// Backbone contract: 7 dependency properties + GoToNextChange / GoToPreviousChange
/// / ShowFind / CopySelection.
/// </summary>
public partial class DiffView : UserControl
{
    public DiffView()
    {
        InitializeComponent();
    }

    #region FileDiff
    public static readonly DependencyProperty FileDiffProperty =
        DependencyProperty.Register(
            nameof(FileDiff), typeof(FileDiff), typeof(DiffView),
            new PropertyMetadata(null, OnRenderInputChanged));

    public FileDiff? FileDiff
    {
        get => (FileDiff?)GetValue(FileDiffProperty);
        set => SetValue(FileDiffProperty, value);
    }
    #endregion

    #region ViewMode
    public static readonly DependencyProperty ViewModeProperty =
        DependencyProperty.Register(
            nameof(ViewMode), typeof(DiffViewMode), typeof(DiffView),
            new PropertyMetadata(DiffViewMode.SideBySide, OnRenderInputChanged));

    public DiffViewMode ViewMode
    {
        get => (DiffViewMode)GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }
    #endregion

    #region ShowWhitespace
    public static readonly DependencyProperty ShowWhitespaceProperty =
        DependencyProperty.Register(
            nameof(ShowWhitespace), typeof(bool), typeof(DiffView),
            new PropertyMetadata(false, OnDisplayOptionChanged));

    public bool ShowWhitespace
    {
        get => (bool)GetValue(ShowWhitespaceProperty);
        set => SetValue(ShowWhitespaceProperty, value);
    }
    #endregion

    #region WordWrap
    public static readonly DependencyProperty WordWrapProperty =
        DependencyProperty.Register(
            nameof(WordWrap), typeof(bool), typeof(DiffView),
            new PropertyMetadata(false, OnDisplayOptionChanged));

    public bool WordWrap
    {
        get => (bool)GetValue(WordWrapProperty);
        set => SetValue(WordWrapProperty, value);
    }
    #endregion

    #region TabSize
    public static readonly DependencyProperty TabSizeProperty =
        DependencyProperty.Register(
            nameof(TabSize), typeof(int), typeof(DiffView),
            new PropertyMetadata(4, OnDisplayOptionChanged));

    public int TabSize
    {
        get => (int)GetValue(TabSizeProperty);
        set => SetValue(TabSizeProperty, value);
    }
    #endregion

    #region SyntaxLanguageId
    public static readonly DependencyProperty SyntaxLanguageIdProperty =
        DependencyProperty.Register(
            nameof(SyntaxLanguageId), typeof(string), typeof(DiffView),
            new PropertyMetadata(null, OnSyntaxChanged));

    public string? SyntaxLanguageId
    {
        get => (string?)GetValue(SyntaxLanguageIdProperty);
        set => SetValue(SyntaxLanguageIdProperty, value);
    }
    #endregion

    #region IsDarkTheme
    public static readonly DependencyProperty IsDarkThemeProperty =
        DependencyProperty.Register(
            nameof(IsDarkTheme), typeof(bool), typeof(DiffView),
            new PropertyMetadata(false, OnSyntaxChanged));

    public bool IsDarkTheme
    {
        get => (bool)GetValue(IsDarkThemeProperty);
        set => SetValue(IsDarkThemeProperty, value);
    }
    #endregion

    // --- Public methods (called by the diff toolbar in the shell) ---
    public void GoToNextChange() { }
    public void GoToPreviousChange() { }
    public void ShowFind() { }
    public void CopySelection() { }

    // --- Change callbacks (wired in later tasks) ---
    private static void OnRenderInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { }
    private static void OnDisplayOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { }
    private static void OnSyntaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { }

    // Editors named in XAML; used by later tasks.
    private TextEditor LeftEditorRef => LeftEditor;
    private TextEditor RightEditorRef => RightEditor;
    private TextEditor UnifiedEditorRef => UnifiedEditor;
}
