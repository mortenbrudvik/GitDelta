using System.Windows;
using GitDelta.Core.Cli;
using GitDelta.UI.ViewModels;
using GitDelta.UI.Views;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GitDelta.UI.Services;

public sealed class ApplicationHostService : IHostedService
{
    private readonly Func<IWindow> _windowFactory;
    private readonly Func<MainWindowViewModel> _viewModelFactory;
    private readonly IThemeService _themeService;
    private readonly LaunchAction _launchAction;
    private readonly ILogger<ApplicationHostService> _logger;

    public ApplicationHostService(
        Func<IWindow> windowFactory,
        Func<MainWindowViewModel> viewModelFactory,
        IThemeService themeService,
        LaunchAction launchAction,
        ILogger<ApplicationHostService> logger)
    {
        _windowFactory = windowFactory;
        _viewModelFactory = viewModelFactory;
        _themeService = themeService;
        _launchAction = launchAction;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Application.Current.Windows.OfType<MainWindow>().Any())
        {
            return Task.CompletedTask;
        }

        // Apply persisted theme before any window is shown to avoid a flash.
        _themeService.ApplyFromSettings();

        var window = _windowFactory();
        var viewModel = _viewModelFactory();
        window.DataContext = viewModel;

        window.Loaded += async (_, _) =>
        {
            try
            {
                // The launch action was parsed once in App.OnStartup and injected here;
                // no re-parse of the command line. The StartAsync cancellation token is
                // intentionally NOT forwarded: it signals host-startup cancellation and is
                // already inert by the time Loaded fires (after the host has started and the
                // window is shown), so passing it would only mask that the initial load is
                // uncancelable. Use None to make that explicit.
                await viewModel.InitializeAsync(_launchAction, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Last-resort guard: the expected failures (git missing/too old, broken repo)
                // are surfaced in-place by the ViewModels; this catches anything unexpected so
                // the user gets a message instead of an unexplained blank window.
                _logger.LogError(ex, "Failed to initialize main window content");
                viewModel.ErrorMessage = $"GitDelta could not start up cleanly: {ex.Message}";
            }
        };

        window.Show();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
