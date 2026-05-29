using GitDelta.Core.Models;

namespace GitDelta.Core.Settings;

/// <summary>
/// WPF-free resolution of <see cref="AppTheme"/> into an effective dark/light decision.
/// Kept in Core so it is unit-testable without referencing WPF-UI.
/// </summary>
public static class ThemeMapping
{
    /// <summary>Resolves whether the effective theme is dark.</summary>
    public static bool ResolveIsDark(AppTheme theme, bool systemIsDark) => theme switch
    {
        AppTheme.Light => false,
        AppTheme.Dark => true,
        _ => systemIsDark,
    };

    /// <summary>Returns the explicit theme that is the opposite of the current effective theme.</summary>
    public static AppTheme Toggle(AppTheme current, bool systemIsDark) =>
        ResolveIsDark(current, systemIsDark) ? AppTheme.Light : AppTheme.Dark;
}
