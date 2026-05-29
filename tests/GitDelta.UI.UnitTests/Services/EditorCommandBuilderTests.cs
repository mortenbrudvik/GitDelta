using GitDelta.UI.Services;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.Services;

public sealed class EditorCommandBuilderTests
{
    [Fact]
    public void TryBuild_SimpleTemplate_WithPlaceholder_SubstitutesPath()
    {
        // Template "code -g {file}" parses to these argv elements.
        string[] templateArgv = ["code", "-g", "{file}"];

        var ok = EditorCommandBuilder.TryBuild(templateArgv, @"C:\repo\Program.cs", out var fileName, out var arguments);

        ok.ShouldBeTrue();
        fileName.ShouldBe("code");
        arguments.ShouldBe(@"-g C:\repo\Program.cs");
    }

    [Fact]
    public void TryBuild_PlaceholderWithSpacedPath_KeepsPathAsSingleQuotedArgument()
    {
        // "code -g {file}" — the {file} token must expand to a single argument even though
        // the substituted path contains spaces.
        string[] templateArgv = ["code", "-g", "{file}"];

        var ok = EditorCommandBuilder.TryBuild(
            templateArgv, @"C:\Users\John Doe\repo\my file.cs", out var fileName, out var arguments);

        ok.ShouldBeTrue();
        fileName.ShouldBe("code");
        arguments.ShouldBe(@"-g ""C:\Users\John Doe\repo\my file.cs""");
    }

    [Fact]
    public void TryBuild_QuotedPlaceholderWithSpacedPath_DoesNotDoubleSplit()
    {
        // User wrote "code -g \"{file}\"" — CommandLineToArgvW collapses the quotes so the
        // token still arrives as a single argv element "{file}". Result must match the bare
        // placeholder case: one quoted argument.
        string[] templateArgv = ["code", "-g", "{file}"];

        var ok = EditorCommandBuilder.TryBuild(
            templateArgv, @"C:\repo\my file.cs", out var fileName, out var arguments);

        ok.ShouldBeTrue();
        fileName.ShouldBe("code");
        arguments.ShouldBe(@"-g ""C:\repo\my file.cs""");
    }

    [Fact]
    public void TryBuild_ExePathWithSpaces_IsUsedAsFileNameUnquoted()
    {
        // The exe path with spaces is a single argv element (quotes already collapsed by the
        // OS parser). It becomes FileName verbatim (ProcessStartInfo.FileName needs no quoting).
        string[] templateArgv = [@"C:\Program Files\Microsoft VS Code\Code.exe", "-g", "{file}"];

        var ok = EditorCommandBuilder.TryBuild(
            templateArgv, @"C:\repo\a.cs", out var fileName, out var arguments);

        ok.ShouldBeTrue();
        fileName.ShouldBe(@"C:\Program Files\Microsoft VS Code\Code.exe");
        arguments.ShouldBe(@"-g C:\repo\a.cs");
    }

    [Fact]
    public void TryBuild_NoPlaceholder_AppendsPathAsTrailingArgument()
    {
        string[] templateArgv = ["notepad"];

        var ok = EditorCommandBuilder.TryBuild(templateArgv, @"C:\repo\a.cs", out var fileName, out var arguments);

        ok.ShouldBeTrue();
        fileName.ShouldBe("notepad");
        arguments.ShouldBe(@"C:\repo\a.cs");
    }

    [Fact]
    public void TryBuild_NoPlaceholderWithSpacedPath_AppendsQuotedPath()
    {
        string[] templateArgv = ["notepad"];

        var ok = EditorCommandBuilder.TryBuild(
            templateArgv, @"C:\Users\John Doe\a.cs", out var fileName, out var arguments);

        ok.ShouldBeTrue();
        fileName.ShouldBe("notepad");
        arguments.ShouldBe(@"""C:\Users\John Doe\a.cs""");
    }

    [Fact]
    public void TryBuild_PlaceholderEmbeddedInArgument_SubstitutesInPlace()
    {
        // e.g. "code -g {file}:1" — token embedded inside an argument.
        string[] templateArgv = ["code", "-g", "{file}:1"];

        var ok = EditorCommandBuilder.TryBuild(templateArgv, @"C:\repo\a.cs", out var fileName, out var arguments);

        ok.ShouldBeTrue();
        fileName.ShouldBe("code");
        arguments.ShouldBe(@"-g C:\repo\a.cs:1");
    }

    [Fact]
    public void TryBuild_EmptyArgv_ReturnsFalse()
    {
        var ok = EditorCommandBuilder.TryBuild([], @"C:\repo\a.cs", out var fileName, out var arguments);

        ok.ShouldBeFalse();
        fileName.ShouldBeNull();
        arguments.ShouldBeEmpty();
    }

    [Fact]
    public void TryBuild_CaseInsensitivePlaceholder_IsSubstituted()
    {
        string[] templateArgv = ["code", "{FILE}"];

        var ok = EditorCommandBuilder.TryBuild(templateArgv, @"C:\repo\a.cs", out var fileName, out var arguments);

        ok.ShouldBeTrue();
        fileName.ShouldBe("code");
        arguments.ShouldBe(@"C:\repo\a.cs");
    }
}
