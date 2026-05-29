using System.IO;
using GitDelta.Core.Models;
using GitDelta.Core.Settings;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _baseDir;

    public JsonSettingsStoreTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "GitDeltaTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_baseDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_baseDir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var store = new JsonSettingsStore(_baseDir);

        var result = store.Load();

        result.ShouldBe(new AppSettings());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllValues()
    {
        var store = new JsonSettingsStore(_baseDir);
        var settings = new AppSettings
        {
            Theme = AppTheme.Dark,
            DefaultDiffView = DiffViewMode.Unified,
            ContextLines = 7,
            TabSize = 2,
            SyntaxHighlighting = false,
            ExternalEditorCommand = "code -g {file}",
            WindowWidth = 1440,
            WindowHeight = 900,
            HistoryPaneWidth = 320,
            FilesPaneWidth = 300,
        };

        store.Save(settings);
        var reloaded = store.Load();

        reloaded.ShouldBe(settings);
    }

    [Fact]
    public void Load_WhenFileCorrupt_ReturnsDefaults()
    {
        var path = Path.Combine(_baseDir, "GitDelta", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ this is not valid json ");
        var store = new JsonSettingsStore(_baseDir);

        var result = store.Load();

        result.ShouldBe(new AppSettings());
    }

    [Fact]
    public void Save_CreatesFileUnderGitDeltaFolder()
    {
        var store = new JsonSettingsStore(_baseDir);

        store.Save(new AppSettings { ContextLines = 5 });

        File.Exists(Path.Combine(_baseDir, "GitDelta", "settings.json")).ShouldBeTrue();
    }
}
