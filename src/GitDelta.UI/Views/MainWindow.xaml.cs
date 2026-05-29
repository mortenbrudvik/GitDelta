using System.ComponentModel;
using System.Windows;
using GitDelta.Core.Models;
using GitDelta.Core.Settings;
using GitDelta.UI.Services;
using GitDelta.UI.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace GitDelta.UI.Views;

public partial class MainWindow : FluentWindow, IWindow
{
    private readonly IThemeService _themeService;
    private readonly ISettingsStore _settingsStore;
    private ShellViewModel? _subscribedShell;
    private bool _watchingSystemTheme;

    public MainWindow(IThemeService themeService, ISettingsStore settingsStore)
    {
        _themeService = themeService;
        _settingsStore = settingsStore;
        InitializeComponent();

        // Follow the OS light/dark setting ONLY while the user's choice is AppTheme.System.
        // SystemThemeWatcher applies the theme out-of-band (bypassing IThemeService), so for an
        // explicit Light/Dark choice it must stay unwatched, otherwise an OS theme change would
        // silently override the user's selection. ThemeService keeps the diff syntax palette in
        // sync via ApplicationThemeManager.Changed whenever the watcher does apply a theme.
        UpdateSystemThemeWatcher();

        DataContextChanged += OnDataContextChanged;
        Closing += OnWindowClosing;
        Loaded += OnWindowLoaded;
    }

    private void OnToggleThemeClick(object sender, RoutedEventArgs e)
    {
        _themeService.Toggle();
        UpdateSystemThemeWatcher();
    }

    // Watch the OS theme only while the user's choice is AppTheme.System; for an explicit
    // Light/Dark choice, unwatch so an OS theme change cannot override the user's selection.
    // The state flag ensures we never UnWatch a window that was never watched (it has no HWND
    // yet during construction, where UnWatch is unsafe) and never re-Watch redundantly. Watch is
    // safe to call pre-HWND: WPF-UI defers the hook to the window's SourceInitialized.
    private void UpdateSystemThemeWatcher()
    {
        var shouldWatch = _settingsStore.Load().Theme == AppTheme.System;
        if (shouldWatch == _watchingSystemTheme)
        {
            return;
        }

        if (shouldWatch)
        {
            SystemThemeWatcher.Watch(this);
        }
        else
        {
            SystemThemeWatcher.UnWatch(this);
        }

        _watchingSystemTheme = shouldWatch;
    }

    // ── DataContext wiring ─────────────────────────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainWindowViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnMainVmPropertyChanged;
        }

        if (e.NewValue is MainWindowViewModel newVm)
        {
            newVm.PropertyChanged += OnMainVmPropertyChanged;
            // Subscribe to the initial content if already set.
            SubscribeToShell(newVm.CurrentContent as ShellViewModel);
        }
    }

    private void OnMainVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.CurrentContent))
        {
            return;
        }

        var mainVm = (MainWindowViewModel)sender!;
        SubscribeToShell(mainVm.CurrentContent as ShellViewModel);
    }

    private void SubscribeToShell(ShellViewModel? shell)
    {
        if (_subscribedShell is not null)
        {
            _subscribedShell.SettingsRequested -= OnSettingsRequested;
            _subscribedShell = null;
        }

        if (shell is null)
        {
            return;
        }

        _subscribedShell = shell;
        shell.SettingsRequested += OnSettingsRequested;
    }

    // ── Settings dialog ────────────────────────────────────────────────────────

    private async void OnSettingsRequested()
    {
        var current = _settingsStore.Load();
        var vm = new SettingsViewModel(current);
        var dialog = new SettingsDialog(RootContentDialogHost, vm);

        var result = await dialog.ShowAsync(CancellationToken.None);
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        vm.SaveCommand.Execute(null);
        var updated = vm.Result;
        _settingsStore.Save(updated);

        // Apply theme immediately, then re-evaluate OS-follow: watch only while System is chosen.
        _themeService.Apply(updated.Theme);
        UpdateSystemThemeWatcher();

        // Propagate diff-related settings to the active shell.
        if (_subscribedShell is not null)
        {
            _subscribedShell.Diff.ViewMode = updated.DefaultDiffView;
            _subscribedShell.Diff.TabSize = updated.TabSize;
        }
    }

    // ── Window size persistence ────────────────────────────────────────────────

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var settings = _settingsStore.Load();

        // Ignore non-finite (NaN/Infinity) or below-minimum persisted sizes so a
        // corrupt stored value cannot break startup.
        if (double.IsFinite(settings.WindowWidth) && settings.WindowWidth >= MinWidth)
        {
            Width = settings.WindowWidth;
        }

        if (double.IsFinite(settings.WindowHeight) && settings.WindowHeight >= MinHeight)
        {
            Height = settings.WindowHeight;
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // Width/Height are Double.NaN when Maximized/Minimized, and System.Text.Json
        // throws on NaN. Only persist a real size captured in the Normal state.
        if (WindowState != WindowState.Normal
            || !double.IsFinite(Width)
            || !double.IsFinite(Height))
        {
            return;
        }

        var current = _settingsStore.Load();
        _settingsStore.Save(current with
        {
            WindowWidth = Width,
            WindowHeight = Height,
        });
    }
}
