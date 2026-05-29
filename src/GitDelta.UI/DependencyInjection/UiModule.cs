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
        // Phase 9 may enhance this with additional UX (last-used folder, etc.).
        builder.RegisterType<WpfFolderPicker>()
            .As<IFolderPicker>()
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
