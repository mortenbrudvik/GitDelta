namespace GitDelta.UI.Services;

/// <summary>
/// Opens a file in an external editor: the user's configured command first, the OS default
/// as a fallback. Encapsulates the process-launching that previously lived in window
/// code-behind so failures can be surfaced (rather than silently swallowed) and unit-tested.
/// </summary>
public interface IExternalEditorLauncher
{
    /// <summary>
    /// Attempts to open <paramref name="absolutePath"/>. Uses <paramref name="editorCommandTemplate"/>
    /// (a command line with a <c>{file}</c> placeholder) when set, otherwise the OS default app.
    /// Returns <c>true</c> if a launch started; <c>false</c> if every attempt failed (already logged).
    /// </summary>
    bool TryOpen(string? editorCommandTemplate, string absolutePath);
}
