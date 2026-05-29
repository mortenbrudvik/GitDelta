using System.Windows.Media;
using GitDelta.Core.Models;
using GitDelta.Core.Settings;
using Wpf.Ui.Appearance;

namespace GitDelta.UI.Services;

public sealed class ThemeService : IThemeService
{
    private readonly ISettingsStore _settings;

    public ThemeService(ISettingsStore settings)
    {
        _settings = settings;

        // ApplicationThemeManager.Changed fires whenever the APPLIED WPF-UI theme changes —
        // including OS-driven changes routed through SystemThemeWatcher, which bypass ApplyCore.
        // Subscribing here keeps IsDark (and therefore the AvalonEdit diff syntax palette, which
        // is driven off IsDarkChanged) in sync no matter who applied the theme. ThemeService is a
        // singleton that lives for the whole app lifetime, so no unsubscribe is needed.
        ApplicationThemeManager.Changed += OnApplicationThemeChanged;
    }

    public bool IsDark { get; private set; }

    public event Action<bool>? IsDarkChanged;

    public void ApplyFromSettings()
    {
        var theme = _settings.Load().Theme;
        ApplyCore(theme);
    }

    public void Apply(AppTheme theme)
    {
        var current = _settings.Load();
        _settings.Save(current with { Theme = theme });
        ApplyCore(theme);
    }

    public void Toggle()
    {
        var current = _settings.Load().Theme;
        var next = ThemeMapping.Toggle(current, SystemIsDark());
        Apply(next);
    }

    private void ApplyCore(AppTheme theme)
    {
        switch (theme)
        {
            case AppTheme.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;
            case AppTheme.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
            default:
                ApplicationThemeManager.ApplySystemTheme();
                break;
        }

        // Sync directly in addition to the Changed event: applying a theme that is already
        // active (e.g. the Dark XAML fallback on first launch) does not raise Changed, so this
        // guarantees IsDark is correct after every explicit apply. SyncIsDark is idempotent, so
        // the extra Changed callback raised for a real switch does not double-fire IsDarkChanged.
        SyncIsDark(ThemeMapping.ResolveIsDark(theme, SystemIsDark()));
    }

    // Updates IsDark and raises IsDarkChanged ONLY on an actual transition, so it is safe to call
    // from both ApplyCore and the ApplicationThemeManager.Changed handler without double-firing.
    private void SyncIsDark(bool resolved)
    {
        if (resolved == IsDark)
        {
            return;
        }

        IsDark = resolved;
        IsDarkChanged?.Invoke(IsDark);
    }

    private void OnApplicationThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccent) =>
        SyncIsDark(currentApplicationTheme == ApplicationTheme.Dark);

    // Reads the actual Windows/OS theme (not the last-applied app theme) so that
    // when Theme==System the effective IsDark reflects the real OS setting.
    private static bool SystemIsDark() =>
        ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark;
}
