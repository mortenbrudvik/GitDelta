using GitDelta.Core.Models;

namespace GitDelta.Core.Settings;

/// <summary>
/// Persists and loads application settings.
/// </summary>
public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
