using GitDelta.UI.Controls.Diff.Syntax;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.Controls;

public sealed class LanguageIdMapTests
{
    [Theory]
    [InlineData("Program.cs", "csharp")]
    [InlineData("app.ts", "typescript")]
    [InlineData("index.tsx", "typescriptreact")]
    [InlineData("main.js", "javascript")]
    [InlineData("data.json", "json")]
    [InlineData("README.md", "markdown")]
    [InlineData("style.css", "css")]
    [InlineData("page.html", "html")]
    [InlineData("build.ps1", "powershell")]
    [InlineData("Dockerfile", "dockerfile")]
    [InlineData("config.yml", "yaml")]
    [InlineData("config.yaml", "yaml")]
    [InlineData("script.py", "python")]
    public void Resolves_known_extensions(string path, string expected)
    {
        LanguageIdMap.FromPath(path).ShouldBe(expected);
    }

    [Fact]
    public void Is_case_insensitive()
    {
        LanguageIdMap.FromPath("PROGRAM.CS").ShouldBe("csharp");
    }

    [Fact]
    public void Unknown_extension_returns_null()
    {
        LanguageIdMap.FromPath("mystery.xyz").ShouldBeNull();
    }

    [Fact]
    public void No_extension_and_unknown_filename_returns_null()
    {
        LanguageIdMap.FromPath("LICENSE").ShouldBeNull();
    }
}
