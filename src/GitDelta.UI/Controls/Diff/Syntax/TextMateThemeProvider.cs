using System.Collections.Concurrent;
using System.Windows.Media;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace GitDelta.UI.Controls.Diff.Syntax;

/// <summary>
/// Wraps the TextMateSharp Registry: resolves a grammar by language id and the
/// DarkPlus/LightPlus theme by IsDarkTheme, and converts theme colors to frozen
/// WPF brushes (cached). Recreated when the theme changes.
/// </summary>
public sealed class TextMateThemeProvider
{
    private readonly RegistryOptions _options;
    private readonly Registry _registry;
    private readonly Theme _theme;
    private readonly ConcurrentDictionary<int, Brush> _brushCache = new();
    private readonly ConcurrentDictionary<string, Brush> _hexCache = new();

    public TextMateThemeProvider(bool isDark)
    {
        IsDark = isDark;
        _options = new RegistryOptions(isDark ? ThemeName.DarkPlus : ThemeName.LightPlus);
        _registry = new Registry(_options);
        _theme = _registry.GetTheme();
    }

    public bool IsDark { get; }

    public Theme Theme => _theme;

    /// <summary>Returns the grammar scope name for a VS Code language id, or null.</summary>
    public string? GetScopeName(string languageId) => _options.GetScopeByLanguageId(languageId);

    /// <summary>Loads the grammar for a language id, or null if unavailable.</summary>
    public IGrammar? LoadGrammar(string languageId)
    {
        string? scope = GetScopeName(languageId);
        return scope is null ? null : _registry.LoadGrammar(scope);
    }

    /// <summary>Resolves a TextMate foreground color id to a frozen brush.</summary>
    public Brush BrushForForeground(int foregroundId)
    {
        return _brushCache.GetOrAdd(foregroundId, id =>
        {
            string? hex = _theme.GetColor(id);
            return BrushFromHex(hex);
        });
    }

    private Brush BrushFromHex(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return Brushes.Transparent;
        }

        return _hexCache.GetOrAdd(hex, h =>
        {
            try
            {
                object? converted = ColorConverter.ConvertFromString(h);
                if (converted is not Color color)
                {
                    return Brushes.Transparent;
                }

                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch (FormatException)
            {
                return Brushes.Transparent;
            }
        });
    }
}
