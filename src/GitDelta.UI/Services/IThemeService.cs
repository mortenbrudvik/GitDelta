using GitDelta.Core.Models;

namespace GitDelta.UI.Services;

public interface IThemeService
{
    /// <summary>True when the effective application theme is dark.</summary>
    bool IsDark { get; }

    /// <summary>Loads the persisted <see cref="AppTheme"/> and applies it via WPF-UI.</summary>
    void ApplyFromSettings();

    /// <summary>Applies a specific theme and persists it.</summary>
    void Apply(AppTheme theme);

    /// <summary>Toggles between explicit light and dark, applies, and persists.</summary>
    void Toggle();

    /// <summary>Raised whenever the effective theme changes.</summary>
    event Action<bool>? IsDarkChanged;
}
