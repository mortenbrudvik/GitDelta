using System.Windows;
using System.Windows.Threading;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using GitDelta.UI.DependencyInjection;
using GitDelta.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GitDelta.UI;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global handler 2/3: exceptions on non-UI threads.
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        // Global handler 3/3: unobserved faulted Tasks.
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _host = Host.CreateDefaultBuilder()
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>(builder =>
            {
                builder.RegisterModule<LoggingModule>();
                builder.RegisterModule<UiModule>();
            })
            .ConfigureServices(services =>
            {
                services.AddHostedService<ApplicationHostService>();
            })
            .Build();

        _host.Start();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        NLog.LogManager.Shutdown();
        base.OnExit(e);
    }

    // Global handler 1/3: exceptions on the WPF UI (Dispatcher) thread.
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        GetLogger()?.LogCritical(e.Exception, "Unhandled UI (dispatcher) exception");

        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "GitDelta",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // Keep the app alive after logging + notifying the user.
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            GetLogger()?.LogCritical(ex, "Unhandled non-UI (AppDomain) exception. Terminating={Terminating}", e.IsTerminating);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        GetLogger()?.LogError(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    private ILogger<App>? GetLogger() =>
        _host?.Services.GetService<ILoggerFactory>()?.CreateLogger<App>();
}
