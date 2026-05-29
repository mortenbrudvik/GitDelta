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

public sealed record AppSettings
{
    public AppTheme Theme { get; init; } = AppTheme.System;
    public DiffViewMode DefaultDiffView { get; init; } = DiffViewMode.SideBySide;
    public int ContextLines { get; init; } = 3;
    public int TabSize { get; init; } = 4;
    public bool SyntaxHighlighting { get; init; } = true;
    public string? ExternalEditorCommand { get; init; }
    public double WindowWidth { get; init; } = 1100;
    public double WindowHeight { get; init; } = 720;
    public double HistoryPaneWidth { get; init; } = 280;
    public double FilesPaneWidth { get; init; } = 260;
}
