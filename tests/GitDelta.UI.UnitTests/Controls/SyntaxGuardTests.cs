using GitDelta.UI.Controls.Diff.Syntax;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.Controls;

public sealed class SyntaxGuardTests
{
    [Fact]
    public void Allows_small_text_with_a_language()
    {
        SyntaxGuard.ShouldTokenize("csharp", lineCount: 100, totalChars: 5_000, isBinary: false)
            .ShouldBeTrue();
    }

    [Fact]
    public void Skips_when_no_language_id()
    {
        SyntaxGuard.ShouldTokenize(null, lineCount: 10, totalChars: 200, isBinary: false)
            .ShouldBeFalse();
    }

    [Fact]
    public void Skips_when_binary()
    {
        SyntaxGuard.ShouldTokenize("csharp", lineCount: 10, totalChars: 200, isBinary: true)
            .ShouldBeFalse();
    }

    [Fact]
    public void Skips_when_too_many_lines()
    {
        SyntaxGuard.ShouldTokenize("csharp", lineCount: SyntaxGuard.MaxLines + 1, totalChars: 1_000, isBinary: false)
            .ShouldBeFalse();
    }

    [Fact]
    public void Skips_when_too_many_chars()
    {
        SyntaxGuard.ShouldTokenize("csharp", lineCount: 10, totalChars: SyntaxGuard.MaxChars + 1, isBinary: false)
            .ShouldBeFalse();
    }

    [Fact]
    public void Allows_exactly_at_the_limits()
    {
        SyntaxGuard.ShouldTokenize("csharp", lineCount: SyntaxGuard.MaxLines, totalChars: SyntaxGuard.MaxChars, isBinary: false)
            .ShouldBeTrue();
    }
}
