using System.IO;
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
    private readonly ILogger<ApplicationHostService> _logger;

    public ApplicationHostService(
        IServiceProvider serviceProvider,
        IThemeService themeService,
        ILogger<ApplicationHostService> logger)
    {
        _serviceProvider = serviceProvider;
        _themeService = themeService;
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

        var window = _serviceProvider.GetRequiredService<MainWindow>();
        var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        window.DataContext = viewModel;

        window.Loaded += async (_, _) =>
        {
            try
            {
                var cliArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
                var action = ArgRouter.Route(cliArgs, Directory.GetCurrentDirectory());
                await viewModel.InitializeAsync(action, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize main window content");
            }
        };

        window.Show();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
