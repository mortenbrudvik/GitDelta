using System.Text.Json;
using System.Text.Json.Serialization;
using GitDelta.Core.Models;

namespace GitDelta.Core.Settings;

/// <summary>
/// Persists application settings as JSON in the user's roaming application data folder
/// (%APPDATA%/GitDelta/settings.json). Returns default settings when the file is missing
/// or corrupt. Enum settings round-trip as strings. Writes via temp-file-then-rename for
/// crash safety. Accepts an injectable base directory for testability.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _settingsPath;
    private readonly Lock _gate = new();

    /// <summary>Default constructor targets %APPDATA%/GitDelta/settings.json.</summary>
    public JsonSettingsStore()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
    {
    }

    /// <summary>Test/seam constructor: <paramref name="baseDirectory"/> stands in for %APPDATA%.</summary>
    public JsonSettingsStore(string baseDirectory)
    {
        _settingsPath = Path.Combine(baseDirectory, "GitDelta", "settings.json");
    }

    public AppSettings Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            try
            {
                string json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            string directory = Path.GetDirectoryName(_settingsPath)!;
            string tempPath = _settingsPath + ".tmp";

            try
            {
                Directory.CreateDirectory(directory);

                string json = JsonSerializer.Serialize(settings, JsonOptions);

                // Write to a temp file then atomically replace the destination, so a crash
                // mid-write can never corrupt the live settings file (a partial .tmp is
                // simply overwritten on the next save).
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _settingsPath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort persistence: Save runs from shutdown, theme toggles, and pane
                // drags, where a disk-full / locked-file / permissions failure must not crash
                // the app. Drop the partial temp file and carry on with the in-memory state.
                TryDeleteTempFile(tempPath);
            }
        }
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing more to do; a stray .tmp is harmless and overwritten on the next save.
        }
    }
}
