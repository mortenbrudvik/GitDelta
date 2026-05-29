using System.IO;

namespace GitDelta.UI.Controls.Diff.Syntax;

/// <summary>
/// Maps a file path to a TextMate language id used to select a grammar.
/// Returns null when no known grammar applies (caller skips highlighting).
/// </summary>
public static class LanguageIdMap
{
    private static readonly IReadOnlyDictionary<string, string> ByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"] = "csharp",
            [".csx"] = "csharp",
            [".ts"] = "typescript",
            [".tsx"] = "typescriptreact",
            [".js"] = "javascript",
            [".jsx"] = "javascriptreact",
            [".mjs"] = "javascript",
            [".cjs"] = "javascript",
            [".json"] = "json",
            [".jsonc"] = "json",
            [".md"] = "markdown",
            [".markdown"] = "markdown",
            [".css"] = "css",
            [".scss"] = "scss",
            [".less"] = "less",
            [".html"] = "html",
            [".htm"] = "html",
            [".xml"] = "xml",
            [".xaml"] = "xml",
            [".ps1"] = "powershell",
            [".psm1"] = "powershell",
            [".yml"] = "yaml",
            [".yaml"] = "yaml",
            [".py"] = "python",
            [".go"] = "go",
            [".rs"] = "rust",
            [".java"] = "java",
            [".c"] = "c",
            [".h"] = "cpp",
            [".cpp"] = "cpp",
            [".hpp"] = "cpp",
            [".sql"] = "sql",
            [".sh"] = "shellscript",
            [".bash"] = "shellscript",
            [".toml"] = "toml",
            [".ini"] = "ini",
        };

    private static readonly IReadOnlyDictionary<string, string> ByFileName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dockerfile"] = "dockerfile",
            [".gitignore"] = "ignore",
            [".gitattributes"] = "ignore",
        };

    public static string? FromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string fileName = Path.GetFileName(path);
        if (ByFileName.TryGetValue(fileName, out string? byName))
        {
            return byName;
        }

        string ext = Path.GetExtension(path);
        if (!string.IsNullOrEmpty(ext) && ByExtension.TryGetValue(ext, out string? byExt))
        {
            return byExt;
        }

        return null;
    }
}
