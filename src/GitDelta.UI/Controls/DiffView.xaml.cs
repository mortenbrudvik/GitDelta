using System.Windows;
using System.Windows.Controls;
using GitDelta.Core.Models;
using GitDelta.UI.Controls.Diff;
using GitDelta.UI.Controls.Diff.Syntax;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

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
        HookSplitScrollSync();
        _editorsConfigured = true;
    }

    private bool _syncingScroll;

    private void HookSplitScrollSync()
    {
        LeftEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) =>
            MirrorScroll(LeftEditor, RightEditor);
        RightEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) =>
            MirrorScroll(RightEditor, LeftEditor);
    }

    private void MirrorScroll(TextEditor source, TextEditor target)
    {
        if (_syncingScroll || ViewMode != DiffViewMode.SideBySide)
        {
            return;
        }

        _syncingScroll = true;
        try
        {
            Vector offset = source.TextArea.TextView.ScrollOffset;
            target.ScrollToVerticalOffset(offset.Y);
            target.ScrollToHorizontalOffset(offset.X);
        }
        finally
        {
            _syncingScroll = false;
        }
    }

    // The built-in search panel installed per editor (used by ShowFind).
    private readonly Dictionary<TextEditor, ICSharpCode.AvalonEdit.Search.SearchPanel> _searchPanels = new();

    private void ConfigureEditor(
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

        // Built-in incremental find (Ctrl+F) + programmatic ShowFind().
        _searchPanels[editor] = ICSharpCode.AvalonEdit.Search.SearchPanel.Install(editor.TextArea);
    }

    private static void OnRenderInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((DiffView)d).Rebuild();
    }

    private static void OnDisplayOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((DiffView)d).ApplyDisplayOptions();
    }

    private static void OnSyntaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((DiffView)d).ApplySyntax();
    }

    private TextMateThemeProvider? _themeProvider;
    private readonly Dictionary<TextEditor, TextMateColorizer> _syntaxColorizers = new();

    private void ApplySyntax()
    {
        ConfigureEditorsOnce();
        ClearSyntax();

        string? langId = SyntaxLanguageId;
        FileDiff? diff = FileDiff;
        if (langId is null || diff is null || diff.IsBinary)
        {
            return;
        }

        _themeProvider = new TextMateThemeProvider(IsDarkTheme);

        // Only colorize the editors that are actually visible for the current mode;
        // adding a colorizer to the hidden editor wastes a grammar/colorizer alloc
        // that is never rendered (review fix #3).
        TextEditor[] activeEditors = ViewMode == DiffViewMode.Unified
            ? new[] { UnifiedEditor }
            : new[] { LeftEditor, RightEditor };

        foreach (TextEditor editor in activeEditors)
        {
            int lineCount = editor.Document.LineCount;
            int chars = editor.Document.TextLength;
            if (!SyntaxGuard.ShouldTokenize(langId, lineCount, chars, diff.IsBinary))
            {
                continue;
            }

            TextMateSharp.Grammars.IGrammar? grammar = _themeProvider.LoadGrammar(langId);
            if (grammar is null)
            {
                continue;
            }

            var colorizer = new TextMateColorizer(_themeProvider, grammar);

            // Insert syntax BEFORE the intra-line colorizer so diff tints layer on top.
            IList<IVisualLineTransformer> transformers = editor.TextArea.TextView.LineTransformers;
            int intraIndex = IndexOfIntra(editor, transformers);
            if (intraIndex >= 0)
            {
                transformers.Insert(intraIndex, colorizer);
            }
            else
            {
                transformers.Add(colorizer);
            }

            _syntaxColorizers[editor] = colorizer;
            editor.TextArea.TextView.Redraw();
        }
    }

    private int IndexOfIntra(TextEditor editor, IList<IVisualLineTransformer> transformers)
    {
        IntraLineColorizer intra = editor == LeftEditor ? _leftIntra
            : editor == RightEditor ? _rightIntra
            : _unifiedIntra;
        return transformers.IndexOf(intra);
    }

    private void ClearSyntax()
    {
        foreach ((TextEditor editor, TextMateColorizer colorizer) in _syntaxColorizers)
        {
            editor.TextArea.TextView.LineTransformers.Remove(colorizer);
            editor.TextArea.TextView.Redraw();
        }

        _syntaxColorizers.Clear();
    }

    // Cached document model for the current FileDiff/ViewMode; rebuilt only when
    // those change (in Rebuild). Navigation reads ActiveSide from this (review fix #1).
    private DiffDocumentModel? _lastModel;

    private void Rebuild()
    {
        ConfigureEditorsOnce();
        FileDiff? diff = FileDiff;

        if (diff is null)
        {
            _lastModel = null;
            ShowOverlay(EmptyState, null);
            return;
        }

        if (diff.IsBinary)
        {
            _lastModel = null;
            ShowOverlay(PlaceholderState, "Binary file — no textual diff");
            return;
        }

        // Real content: hide overlays, build documents.
        EmptyState.Visibility = Visibility.Collapsed;
        PlaceholderState.Visibility = Visibility.Collapsed;

        // Build once per FileDiff/ViewMode change and cache so navigation does not
        // re-run the O(N) builder on every keypress (review fix #1).
        DiffDocumentModel model = DiffDocumentBuilder.Build(diff, ViewMode);
        _lastModel = model;

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

        // Re-apply syntax so a freshly built document gets highlighted.
        ApplySyntax();
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

    private TextEditor ActiveEditor =>
        ViewMode == DiffViewMode.Unified ? UnifiedEditor : RightEditor;

    private DiffDocumentSide? ActiveSide =>
        ViewMode == DiffViewMode.Unified ? _lastModel?.Unified : _lastModel?.Right;

    // --- Public methods (called by the diff toolbar in the shell) ---
    public void GoToNextChange()
    {
        NavigateChange(forward: true);
    }

    public void GoToPreviousChange()
    {
        NavigateChange(forward: false);
    }

    public void ShowFind()
    {
        ConfigureEditorsOnce();
        TextEditor editor = ActiveEditor;
        editor.Focus();
        if (_searchPanels.TryGetValue(editor, out ICSharpCode.AvalonEdit.Search.SearchPanel? panel))
        {
            panel.Open();
        }
    }

    public void CopySelection()
    {
        TextEditor editor = ActiveEditor;
        if (!string.IsNullOrEmpty(editor.SelectedText))
        {
            editor.Copy();
        }
    }

    private void NavigateChange(bool forward)
    {
        DiffDocumentSide? side = ActiveSide;
        TextEditor editor = ActiveEditor;
        if (side is null || side.Rows.Count == 0)
        {
            return;
        }

        int currentRow = editor.TextArea.Caret.Line - 1; // 0-based row index
        int count = side.Rows.Count;
        int step = forward ? 1 : -1;

        for (int n = 1; n <= count; n++)
        {
            int idx = currentRow + (step * n);
            if (idx < 0 || idx >= count)
            {
                break;
            }

            DiffRowKind kind = side.Rows[idx].Kind;
            if (kind is DiffRowKind.Added or DiffRowKind.Deleted or DiffRowKind.Modified)
            {
                int docLine = idx + 1;
                editor.ScrollToLine(docLine);
                editor.TextArea.Caret.Line = docLine;
                editor.TextArea.Caret.Column = 1;
                editor.TextArea.Caret.BringCaretToView();
                return;
            }
        }
    }
}
