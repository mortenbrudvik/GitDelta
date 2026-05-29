using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using GitDelta.UI.Views;
using Wpf.Ui.Appearance;

namespace GitDelta.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);

        // Match the OS light/dark setting before the window is shown so there is no theme flash.
        ApplicationThemeManager.ApplySystemTheme();

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            // TODO (Phase 5): replace Trace with ILogger.
            Trace.TraceError("Unhandled AppDomain exception: {0}", ex);

            MessageBox.Show(
                $"An unexpected error occurred: {ex.Message}",
                "GitDelta",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // TODO (Phase 5): replace Trace with ILogger.
        Trace.TraceError("Unobserved task exception: {0}", e.Exception);
        e.SetObserved();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // TODO (Phase 5): replace Trace with ILogger.
        Trace.TraceError("Unhandled dispatcher exception: {0}", e.Exception);

        MessageBox.Show(
            $"An unexpected error occurred: {e.Exception.Message}",
            "GitDelta",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
