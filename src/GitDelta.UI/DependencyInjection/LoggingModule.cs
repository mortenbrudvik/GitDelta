using System.IO;
using Autofac;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace GitDelta.UI.DependencyInjection;

public sealed class LoggingModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GitDelta", "Logs");
        Directory.CreateDirectory(logDir);

        var config = new NLog.Config.LoggingConfiguration();
        var fileTarget = new NLog.Targets.FileTarget("file")
        {
            FileName = Path.Combine(logDir, "gitdelta-${shortdate}.log"),
            Layout = "${longdate} ${level:uppercase=true} ${logger} ${message} ${exception:format=tostring}",
            MaxArchiveFiles = 14,
            ArchiveAboveSize = 5_000_000,
        };
        var asyncTarget = new NLog.Targets.Wrappers.AsyncTargetWrapper(fileTarget);
        config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, asyncTarget);
        NLog.LogManager.Configuration = config;

        builder.Register(_ =>
            {
                var factory = LoggerFactory.Create(b => b.AddNLog());
                return factory;
            })
            .As<ILoggerFactory>()
            .SingleInstance();

        // ILogger<T> defaults to InstancePerDependency (the conventional MEL
        // lifetime); the underlying ILoggerFactory remains SingleInstance.
        builder.RegisterGeneric(typeof(Logger<>))
            .As(typeof(ILogger<>));
    }
}
