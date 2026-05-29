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
            Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(settings, JsonOptions);
            string tempPath = _settingsPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _settingsPath, overwrite: true);
        }
    }
}
