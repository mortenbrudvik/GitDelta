using System.IO;
using System.Windows;
using System.Windows.Threading;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using GitDelta.Core.Cli;
using GitDelta.UI.Cli;
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

        // Route args ONCE — before building the host or showing any window.
        // e.Args excludes the exe path; cwd is the launching directory. This is the
        // single source of truth; the host receives it via DI (no re-parse).
        var action = ArgRouter.Route(e.Args, Directory.GetCurrentDirectory());

        // Console-only fast paths: write to the parent terminal and exit without a window.
        if (action.Kind is LaunchActionKind.PrintHelp or LaunchActionKind.PrintVersion)
        {
            // Broken pipe (e.g. `gitdelta --version | head -1`) or a missing console
            // surfaces as IOException from the AutoFlush writer; swallow it so the
            // process exits cleanly instead of raising a WER crash dialog. This runs
            // before the global handlers are wired, so it must guard itself.
            try
            {
                RunConsoleMode(action);
            }
            catch (IOException)
            {
                // Broken pipe / no console — nothing to do but exit cleanly.
            }

            Shutdown();
            return;
        }

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
                // Hand the already-parsed launch action to the host so
                // ApplicationHostService consumes it instead of re-parsing.
                services.AddSingleton(action);
                services.AddHostedService<ApplicationHostService>();
            })
            .Build();

        _host.Start();
    }

    /// <summary>
    /// Handles the console-only fast paths (--help, --version).
    /// Attaches to the parent terminal if available; writes the requested text;
    /// then detaches. If there is no parent console (e.g. launched from Explorer),
    /// AttachConsole returns false and we silently exit.
    /// </summary>
    private static void RunConsoleMode(LaunchAction action)
    {
        var attached = NativeConsole.TryAttachToParentConsole();

        if (action.Kind == LaunchActionKind.PrintHelp)
        {
            ConsoleOutput.WriteHelp();
        }
        else
        {
            ConsoleOutput.WriteVersion();
        }

        if (attached)
        {
            Console.Out.Flush();
            NativeConsole.Detach();
        }
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
