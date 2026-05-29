using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using TextMateSharp.Grammars;

namespace GitDelta.UI.Controls.Diff.Syntax;

/// <summary>
/// Hand-written TextMate -> AvalonEdit adapter (spec §15). Tokenizes each visible
/// line with IGrammar.TokenizeLine, threads grammar state line-to-line, and maps
/// each token's scopes to a theme foreground brush. Added to LineTransformers
/// BEFORE the intra-line colorizer so diff tints layer over syntax foregrounds.
/// </summary>
public sealed class TextMateColorizer : DocumentColorizingTransformer
{
    private readonly TextMateThemeProvider _provider;
    private readonly IGrammar _grammar;

    // Cache of start-state per document line so re-tokenizing a single visible line
    // can resume from the prior line's state. Index is 1-based line number.
    private readonly Dictionary<int, IStateStack?> _lineStartState = new();

    public TextMateColorizer(TextMateThemeProvider provider, IGrammar grammar)
    {
        _provider = provider;
        _grammar = grammar;
        _lineStartState[1] = null; // first line starts from the null state
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        string text = CurrentContext.Document.GetText(line);
        IStateStack? startState = GetStartState(line);

        ITokenizeLineResult result = _grammar.TokenizeLine(text, startState, TimeSpan.MaxValue);

        // Remember the end state as the next line's start state.
        _lineStartState[line.LineNumber + 1] = result.RuleStack;

        int lineStart = line.Offset;
        foreach (IToken token in result.Tokens)
        {
            int tokenStart = lineStart + Math.Min(token.StartIndex, text.Length);
            int tokenEnd = lineStart + Math.Min(token.EndIndex, text.Length);
            if (tokenEnd <= tokenStart)
            {
                continue;
            }

            int foreground = ResolveForeground(token.Scopes);
            if (foreground <= 0)
            {
                continue;
            }

            System.Windows.Media.Brush brush = _provider.BrushForForeground(foreground);
            ChangeLinePart(tokenStart, tokenEnd, el => el.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    private IStateStack? GetStartState(DocumentLine line)
    {
        if (_lineStartState.TryGetValue(line.LineNumber, out IStateStack? state))
        {
            return state;
        }

        // Tokenize all preceding lines once to build the state chain up to this line.
        IStateStack? running = null;
        for (int n = 1; n < line.LineNumber; n++)
        {
            DocumentLine prior = CurrentContext.Document.GetLineByNumber(n);
            string priorText = CurrentContext.Document.GetText(prior);
            ITokenizeLineResult priorResult = _grammar.TokenizeLine(priorText, running, TimeSpan.MaxValue);
            running = priorResult.RuleStack;
            _lineStartState[n + 1] = running;
        }

        return running;
    }

    private int ResolveForeground(IReadOnlyList<string> scopes)
    {
        // Most-specific scope last; ask the theme for its style and take the foreground.
        for (int i = scopes.Count - 1; i >= 0; i--)
        {
            List<TextMateSharp.Themes.ThemeTrieElementRule> rules =
                _provider.Theme.Match(new[] { scopes[i] });
            foreach (TextMateSharp.Themes.ThemeTrieElementRule rule in rules)
            {
                if (rule.foreground > 0)
                {
                    return rule.foreground;
                }
            }
        }

        return 0;
    }
}
