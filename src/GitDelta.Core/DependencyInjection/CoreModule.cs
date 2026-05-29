using Autofac;
using GitDelta.Core.Diff;
using GitDelta.Core.Git;
using GitDelta.Core.Settings;

namespace GitDelta.Core.DependencyInjection;

/// <summary>
/// Autofac registrations for the WPF-free Core layer: git access, intra-line diffing, settings.
/// Services are SingleInstance; constructor injection only.
/// </summary>
public sealed class CoreModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<GitProcessRunner>()
            .As<IGitProcessRunner>()
            .SingleInstance();

        builder.RegisterType<DiffPlexIntraLineDiffer>()
            .As<IIntraLineDiffer>()
            .SingleInstance();

        builder.RegisterType<CliGitReader>()
            .As<IGitReader>()
            .SingleInstance();

        builder.RegisterType<JsonSettingsStore>()
            .As<ISettingsStore>()
            .SingleInstance();
    }
}
