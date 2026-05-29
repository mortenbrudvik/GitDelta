namespace GitDelta.Core.Models;

public enum DiffViewMode
{
    SideBySide,
    Unified
}

public enum AppTheme
{
    System,
    Light,
    Dark
}

/// <summary>
/// Persisted application settings. Numeric members clamp on <c>init</c> so a hand-edited or
/// corrupt settings.json (negative context lines, zero tab size, non-finite window sizes)
/// can never feed an invalid value into <c>git diff -U{n}</c>, the diff renderer, or the layout.
/// </summary>
public sealed record AppSettings
{
    private const int DefaultWindowWidth = 1100;
    private const int DefaultWindowHeight = 720;
    private const int DefaultHistoryPaneWidth = 280;
    private const int DefaultFilesPaneWidth = 260;
    private const double MinWindowDimension = 200;
    private const double MaxWindowDimension = 20_000;
    private const double MinPaneWidth = 50;
    private const double MaxPaneWidth = 4_000;

    public AppTheme Theme { get; init; } = AppTheme.System;
    public DiffViewMode DefaultDiffView { get; init; } = DiffViewMode.SideBySide;

    private readonly int _contextLines = 3;
    public int ContextLines
    {
        get => _contextLines;
        init => _contextLines = Math.Clamp(value, 0, 50);
    }

    private readonly int _tabSize = 4;
    public int TabSize
    {
        get => _tabSize;
        init => _tabSize = Math.Clamp(value, 1, 16);
    }

    public bool SyntaxHighlighting { get; init; } = true;
    public string? ExternalEditorCommand { get; init; }

    private readonly double _windowWidth = DefaultWindowWidth;
    public double WindowWidth
    {
        get => _windowWidth;
        init => _windowWidth = Sanitize(value, DefaultWindowWidth, MinWindowDimension, MaxWindowDimension);
    }

    private readonly double _windowHeight = DefaultWindowHeight;
    public double WindowHeight
    {
        get => _windowHeight;
        init => _windowHeight = Sanitize(value, DefaultWindowHeight, MinWindowDimension, MaxWindowDimension);
    }

    private readonly double _historyPaneWidth = DefaultHistoryPaneWidth;
    public double HistoryPaneWidth
    {
        get => _historyPaneWidth;
        init => _historyPaneWidth = Sanitize(value, DefaultHistoryPaneWidth, MinPaneWidth, MaxPaneWidth);
    }

    private readonly double _filesPaneWidth = DefaultFilesPaneWidth;
    public double FilesPaneWidth
    {
        get => _filesPaneWidth;
        init => _filesPaneWidth = Sanitize(value, DefaultFilesPaneWidth, MinPaneWidth, MaxPaneWidth);
    }

    /// <summary>Replaces a non-finite value with <paramref name="fallback"/>, else clamps to range.</summary>
    private static double Sanitize(double value, double fallback, double min, double max)
        => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
