using System.ComponentModel;
using System.Windows;
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

    public MainWindow(IThemeService themeService, ISettingsStore settingsStore)
    {
        _themeService = themeService;
        _settingsStore = settingsStore;
        InitializeComponent();

        // Track Windows light/dark changes while AppTheme.System is in effect.
        SystemThemeWatcher.Watch(this);

        DataContextChanged += OnDataContextChanged;
        Closing += OnWindowClosing;
        Loaded += OnWindowLoaded;
    }

    private void OnToggleThemeClick(object sender, RoutedEventArgs e)
    {
        _themeService.Toggle();
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

        // Apply theme immediately.
        _themeService.Apply(updated.Theme);

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
