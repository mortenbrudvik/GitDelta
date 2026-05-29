using GitDelta.UI.Cli;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.Cli;

/// <summary>
/// Unit tests for <see cref="CliHelpText"/>. Only the pure string properties are
/// covered here; the console attach/write path (NativeConsole) requires a real
/// terminal and is verified manually.
/// </summary>
public class CliHelpTextTests
{
    // ── Help text ────────────────────────────────────────────────────────────

    [Fact]
    public void Help_ContainsUsageKeyword()
    {
        CliHelpText.Help.ShouldContain("Usage", Case.Insensitive);
    }

    [Fact]
    public void Help_ContainsHelpFlag()
    {
        CliHelpText.Help.ShouldContain("--help");
    }

    [Fact]
    public void Help_ContainsVersionFlag()
    {
        CliHelpText.Help.ShouldContain("--version");
    }

    [Fact]
    public void Help_DescribesPathArgument()
    {
        // Opening a repo at a path is the key working-tree fast-path.
        CliHelpText.Help.ShouldContain("<path>", Case.Insensitive);
    }

    [Fact]
    public void Help_IsNotEmpty()
    {
        CliHelpText.Help.ShouldNotBeNullOrWhiteSpace();
    }

    // ── Version string ───────────────────────────────────────────────────────

    [Fact]
    public void Version_IsNotEmpty()
    {
        CliHelpText.Version.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Version_StartsWithProductName()
    {
        CliHelpText.Version.ShouldStartWith("gitdelta ");
    }

    [Fact]
    public void Version_DoesNotContainBuildMetadataSuffix()
    {
        // The '+' separator and any hash/metadata after it should be stripped.
        CliHelpText.Version.ShouldNotContain("+");
    }

    [Fact]
    public void Version_ContainsParsableVersionNumber()
    {
        // After stripping the "gitdelta " prefix, the remainder should be a valid
        // dotted-numeric version (e.g. "1.0.0" or "0.1.0").
        var raw = CliHelpText.Version;
        raw.ShouldStartWith("gitdelta ");
        var versionPart = raw["gitdelta ".Length..];
        Version.TryParse(versionPart, out var parsed).ShouldBeTrue(
            $"Version part '{versionPart}' could not be parsed as a System.Version");
        parsed!.Major.ShouldBeGreaterThanOrEqualTo(0);
    }
}
