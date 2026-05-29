using System.Windows;
using GitDelta.UI.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace GitDelta.UI.Views;

public partial class MainWindow : FluentWindow, IWindow
{
    private readonly IThemeService _themeService;

    public MainWindow(IThemeService themeService)
    {
        _themeService = themeService;
        InitializeComponent();

        // Track Windows light/dark changes while AppTheme.System is in effect.
        SystemThemeWatcher.Watch(this);
    }

    private void OnToggleThemeClick(object sender, RoutedEventArgs e)
    {
        _themeService.Toggle();
    }
}
