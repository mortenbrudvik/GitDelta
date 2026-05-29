using System.Windows;
using System.Windows.Controls;
using GitDelta.Core.Models;
using GitDelta.UI.Controls.Diff;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

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

    // One renderer/margin/colorizer per editor surface.
    private readonly DiffLineBackgroundRenderer _leftBg = new();
    private readonly DiffLineBackgroundRenderer _rightBg = new();
    private readonly DiffLineBackgroundRenderer _unifiedBg = new();

    private readonly ChangeBarMargin _leftBar = new();
    private readonly ChangeBarMargin _rightBar = new();
    private readonly ChangeBarMargin _unifiedBar = new();

    private readonly IntraLineColorizer _leftIntra = new();
    private readonly IntraLineColorizer _rightIntra = new();
    private readonly IntraLineColorizer _unifiedIntra = new();

    private bool _editorsConfigured;

    private void ConfigureEditorsOnce()
    {
        if (_editorsConfigured)
        {
            return;
        }

        ConfigureEditor(LeftEditor, _leftBg, _leftBar, _leftIntra);
        ConfigureEditor(RightEditor, _rightBg, _rightBar, _rightIntra);
        ConfigureEditor(UnifiedEditor, _unifiedBg, _unifiedBar, _unifiedIntra);
        _editorsConfigured = true;
    }

    private static void ConfigureEditor(
        TextEditor editor,
        DiffLineBackgroundRenderer bg,
        ChangeBarMargin bar,
        IntraLineColorizer intra)
    {
        // Read-only, no undo/edit subsystem cost (spec §8).
        editor.IsReadOnly = true;
        editor.Document = new TextDocument();
        editor.Document.UndoStack.SizeLimit = 0;
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        editor.Options.AllowScrollBelowDocument = false;

        editor.TextArea.TextView.BackgroundRenderers.Add(bg);
        editor.TextArea.LeftMargins.Insert(0, bar);
        editor.TextArea.TextView.LineTransformers.Add(intra); // intra last for now; syntax inserts before it in Phase 8
    }

    private static void OnRenderInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((DiffView)d).Rebuild();
    }

    private static void OnDisplayOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((DiffView)d).ApplyDisplayOptions();
    }

    private static void OnSyntaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { }

    private void Rebuild()
    {
        ConfigureEditorsOnce();
        FileDiff? diff = FileDiff;

        if (diff is null)
        {
            ShowOverlay(EmptyState, null);
            return;
        }

        if (diff.IsBinary)
        {
            ShowOverlay(PlaceholderState, "Binary file — no textual diff");
            return;
        }

        // Real content: hide overlays, build documents.
        EmptyState.Visibility = Visibility.Collapsed;
        PlaceholderState.Visibility = Visibility.Collapsed;

        DiffDocumentModel model = DiffDocumentBuilder.Build(diff, ViewMode);

        if (ViewMode == DiffViewMode.Unified)
        {
            SplitRoot.Visibility = Visibility.Collapsed;
            UnifiedEditor.Visibility = Visibility.Visible;
            LoadSide(UnifiedEditor, _unifiedBg, _unifiedBar, _unifiedIntra, model.Unified);
        }
        else
        {
            UnifiedEditor.Visibility = Visibility.Collapsed;
            SplitRoot.Visibility = Visibility.Visible;
            LoadSide(LeftEditor, _leftBg, _leftBar, _leftIntra, model.Left);
            LoadSide(RightEditor, _rightBg, _rightBar, _rightIntra, model.Right);
        }

        ApplyDisplayOptions();

        if (diff.IsTruncated)
        {
            // Show overlay text but keep the (partial) diff visible behind it as a banner.
            PlaceholderState.Visibility = Visibility.Visible;
            PlaceholderState.VerticalAlignment = VerticalAlignment.Bottom;
            PlaceholderState.Text = "Diff truncated — file too large to show in full";
        }
    }

    private static void LoadSide(
        TextEditor editor,
        DiffLineBackgroundRenderer bg,
        ChangeBarMargin bar,
        IntraLineColorizer intra,
        DiffDocumentSide? side)
    {
        editor.Document.Text = side?.Text ?? string.Empty;
        bg.SetSide(side);
        bar.SetSide(side);
        intra.SetSide(side);
        editor.TextArea.TextView.Redraw();
    }

    private void ShowOverlay(TextBlock overlay, string? text)
    {
        SplitRoot.Visibility = Visibility.Collapsed;
        UnifiedEditor.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Collapsed;
        PlaceholderState.Visibility = Visibility.Collapsed;

        overlay.VerticalAlignment = VerticalAlignment.Center;
        if (text is not null)
        {
            overlay.Text = text;
        }

        overlay.Visibility = Visibility.Visible;
    }

    private void ApplyDisplayOptions()
    {
        ConfigureEditorsOnce();
        bool wrap = WordWrap && ViewMode == DiffViewMode.Unified; // word-wrap disabled in split (spec §8)
        foreach (TextEditor editor in new[] { LeftEditor, RightEditor, UnifiedEditor })
        {
            editor.WordWrap = wrap;
            editor.Options.ShowSpaces = ShowWhitespace;
            editor.Options.ShowTabs = ShowWhitespace;
            editor.Options.IndentationSize = TabSize < 1 ? 4 : TabSize;
        }
    }

    // --- Public methods (called by the diff toolbar in the shell) ---
    public void GoToNextChange() { }
    public void GoToPreviousChange() { }
    public void ShowFind() { }
    public void CopySelection() { }
}
