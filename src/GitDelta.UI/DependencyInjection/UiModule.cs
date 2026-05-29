using Autofac;
using GitDelta.Core.DependencyInjection;
using GitDelta.UI.Services;
using GitDelta.UI.ViewModels;
using GitDelta.UI.Views;

namespace GitDelta.UI.DependencyInjection;

public sealed class UiModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Compose the Core layer (IGitReader, IIntraLineDiffer, ISettingsStore, etc.).
        builder.RegisterModule<CoreModule>();

        // UI services — SingleInstance.
        builder.RegisterType<ThemeService>()
            .As<IThemeService>()
            .SingleInstance();

        // IFolderPicker — WPF implementation using OpenFolderDialog (.NET 10).
        builder.RegisterType<WpfFolderPicker>()
            .As<IFolderPicker>()
            .SingleInstance();

        // External-editor launching (custom command + OS-default fallback), behind an
        // IProcessStarter seam so the decision logic is unit-tested.
        builder.RegisterType<ProcessStarter>()
            .As<IProcessStarter>()
            .SingleInstance();

        builder.RegisterType<ExternalEditorLauncher>()
            .As<IExternalEditorLauncher>()
            .SingleInstance();

        // ViewModels — InstancePerDependency (transient). Func<T> factories are
        // auto-provided by Autofac for the container/child-VM creation patterns.
        // SettingsViewModel is not registered: MainWindow constructs it directly
        // with the freshly loaded AppSettings each time the dialog opens.
        builder.RegisterType<MainWindowViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<StartViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<ShellViewModel>().AsSelf().InstancePerDependency();

        // Main window — SingleInstance.
        builder.RegisterType<MainWindow>()
            .AsSelf()
            .As<IWindow>()
            .SingleInstance();
    }
}
