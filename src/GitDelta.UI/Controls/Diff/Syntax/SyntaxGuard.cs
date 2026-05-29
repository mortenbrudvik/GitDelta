namespace GitDelta.UI.Controls.Diff.Syntax;

/// <summary>
/// Decides whether syntax tokenization should run for a document. Large or binary
/// content falls back to monochrome (spec §8 performance valve).
/// </summary>
public static class SyntaxGuard
{
    public const int MaxLines = 20_000;
    public const int MaxChars = 2_000_000;

    public static bool ShouldTokenize(string? languageId, int lineCount, int totalChars, bool isBinary)
    {
        if (isBinary || string.IsNullOrEmpty(languageId))
        {
            return false;
        }

        if (lineCount > MaxLines || totalChars > MaxChars)
        {
            return false;
        }

        return true;
    }
}
