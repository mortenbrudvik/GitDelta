using System.ComponentModel;
using System.Diagnostics;
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
            _subscribedShell.EditorRequested -= OnEditorRequested;
            _subscribedShell = null;
        }

        if (shell is null)
        {
            return;
        }

        _subscribedShell = shell;
        shell.SettingsRequested += OnSettingsRequested;
        shell.EditorRequested += OnEditorRequested;
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

    // ── Open-in-editor wiring ──────────────────────────────────────────────────

    private void OnEditorRequested(string absolutePath)
    {
        var settings = _settingsStore.Load();
        var command = settings.ExternalEditorCommand;

        if (!string.IsNullOrWhiteSpace(command))
        {
            TryLaunchEditor(command, absolutePath);
        }
        else
        {
            // Fall back: let the OS open the file with its default application.
            try
            {
                Process.Start(new ProcessStartInfo(absolutePath) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // If OS default also fails, silently ignore — there is nothing useful we can
                // do without a full notification stack at this stage.
            }
        }
    }

    private static void TryLaunchEditor(string commandTemplate, string filePath)
    {
        // Substitute {file} placeholder; fall back to appending the path.
        var expanded = commandTemplate.Contains("{file}", StringComparison.OrdinalIgnoreCase)
            ? commandTemplate.Replace("{file}", filePath, StringComparison.OrdinalIgnoreCase)
            : $"{commandTemplate} \"{filePath}\"";

        // Split on the first space to get exe + args.
        var spaceIndex = expanded.IndexOf(' ');
        string exe, args;
        if (spaceIndex < 0)
        {
            exe = expanded;
            args = string.Empty;
        }
        else
        {
            exe = expanded[..spaceIndex];
            args = expanded[(spaceIndex + 1)..];
        }

        try
        {
            Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Fall back to OS shell open if the custom command fails.
            try
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // Silent ignore.
            }
        }
    }

    // ── Window size persistence ────────────────────────────────────────────────

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var settings = _settingsStore.Load();
        if (settings.WindowWidth >= MinWidth)
        {
            Width = settings.WindowWidth;
        }

        if (settings.WindowHeight >= MinHeight)
        {
            Height = settings.WindowHeight;
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        var current = _settingsStore.Load();
        _settingsStore.Save(current with
        {
            WindowWidth = Width,
            WindowHeight = Height,
        });
    }
}
