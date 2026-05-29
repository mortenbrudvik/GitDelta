using System.Windows;
using GitDelta.Core.Cli;
using GitDelta.UI.ViewModels;
using GitDelta.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GitDelta.UI.Services;

public sealed class ApplicationHostService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IThemeService _themeService;
    private readonly LaunchAction _launchAction;
    private readonly ILogger<ApplicationHostService> _logger;

    public ApplicationHostService(
        IServiceProvider serviceProvider,
        IThemeService themeService,
        LaunchAction launchAction,
        ILogger<ApplicationHostService> logger)
    {
        _serviceProvider = serviceProvider;
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

        var window = _serviceProvider.GetRequiredService<IWindow>();
        var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        window.DataContext = viewModel;

        window.Loaded += async (_, _) =>
        {
            try
            {
                // The launch action was parsed once in App.OnStartup and injected here;
                // no re-parse of the command line.
                await viewModel.InitializeAsync(_launchAction, cancellationToken);
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
