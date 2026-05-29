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

        var resolved = ThemeMapping.ResolveIsDark(theme, SystemIsDark());
        if (resolved != IsDark)
        {
            IsDark = resolved;
            IsDarkChanged?.Invoke(IsDark);
        }
        else
        {
            IsDark = resolved;
        }
    }

    private static bool SystemIsDark() =>
        ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;
}
