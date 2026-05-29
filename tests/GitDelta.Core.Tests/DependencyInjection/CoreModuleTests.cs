using Autofac;
using GitDelta.Core.DependencyInjection;
using GitDelta.Core.Diff;
using GitDelta.Core.Git;
using GitDelta.Core.Settings;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.DependencyInjection;

public class CoreModuleTests
{
    private static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<CoreModule>();
        // Override the real process runner so no git process is ever started.
        builder.RegisterInstance(Substitute.For<IGitProcessRunner>())
               .As<IGitProcessRunner>();
        return builder.Build();
    }

    [Fact]
    public void Module_Resolves_IGitReader()
    {
        using var container = BuildContainer();

        container.Resolve<IGitReader>().ShouldBeOfType<CliGitReader>();
    }

    [Fact]
    public void Module_Resolves_IIntraLineDiffer()
    {
        using var container = BuildContainer();

        container.Resolve<IIntraLineDiffer>().ShouldBeOfType<DiffPlexIntraLineDiffer>();
    }

    [Fact]
    public void Module_Resolves_ISettingsStore()
    {
        using var container = BuildContainer();

        container.Resolve<ISettingsStore>().ShouldBeOfType<JsonSettingsStore>();
    }

    [Fact]
    public void Module_Services_AreSingleInstance()
    {
        using var container = BuildContainer();

        var a = container.Resolve<IGitReader>();
        var b = container.Resolve<IGitReader>();

        a.ShouldBeSameAs(b);
    }
}
