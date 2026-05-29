using GitDelta.Core.Models;

namespace GitDelta.Core.Settings;

/// <summary>
/// Persists and loads application settings.
/// </summary>
public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}
