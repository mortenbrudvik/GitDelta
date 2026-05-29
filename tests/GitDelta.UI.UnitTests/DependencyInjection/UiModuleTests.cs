using Autofac;
using GitDelta.Core.Git;
using GitDelta.UI.DependencyInjection;
using GitDelta.UI.Services;
using GitDelta.UI.ViewModels;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.DependencyInjection;

public class UiModuleTests
{
    private static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<UiModule>();
        // UiModule composes CoreModule (which registers the real git CLI reader). Override the
        // process runner so the container can never start a git process, mirroring CoreModuleTests.
        builder.RegisterInstance(Substitute.For<IGitProcessRunner>())
               .As<IGitProcessRunner>();
        return builder.Build();
    }

    // These guard the ApplicationHostService composition-root contract: it depends on Autofac
    // auto-providing Func<T> factories (constructor injection) rather than resolving services
    // from IServiceProvider. A missing/incorrect registration would not fail the build — it would
    // only surface as a crash at app startup — so it is asserted here.

    [Fact]
    public void Module_Provides_WindowFactory()
    {
        using var container = BuildContainer();

        // Resolve the FACTORY only — do not invoke it. Invoking constructs the FluentWindow,
        // which requires an STA thread and a running Application. Successful resolution proves
        // IWindow is registered and the Func<IWindow> ApplicationHostService injects is available.
        container.Resolve<Func<IWindow>>().ShouldNotBeNull();
    }

    [Fact]
    public void Module_Provides_MainWindowViewModelFactory()
    {
        using var container = BuildContainer();

        container.Resolve<Func<MainWindowViewModel>>().ShouldNotBeNull();
    }

    [Fact]
    public void Module_Registers_HostStartup_Contracts()
    {
        using var container = BuildContainer();

        // Both contracts the host resolves at startup must be registered. (Lifetimes are not
        // asserted here because verifying singleton vs. transient requires invoking the
        // factories, which constructs WPF objects that need an STA thread.)
        container.IsRegistered<IWindow>().ShouldBeTrue();
        container.IsRegistered<MainWindowViewModel>().ShouldBeTrue();
    }
}
