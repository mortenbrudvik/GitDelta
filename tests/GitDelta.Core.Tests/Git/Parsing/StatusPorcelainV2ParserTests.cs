using System.Text;
using GitDelta.Core.Git.Parsing;
using GitDelta.Core.Models;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Git.Parsing;

public class StatusPorcelainV2ParserTests
{
    private const string NUL = "\0";

    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Parse_Untracked_QuestionMarkLine_MapsToUntracked()
    {
        // "? newfile.txt\0"
        var stdout = Bytes("? newfile.txt" + NUL);

        var files = StatusPorcelainV2Parser.Parse(stdout);

        files.Count.ShouldBe(1);
        files[0].Kind.ShouldBe(ChangeKind.Untracked);
        files[0].Path.ShouldBe("newfile.txt");
    }

    [Fact]
    public void Parse_OrdinaryModified_TypeOneLine_MapsToModified()
    {
        // "1 .M N... 100644 100644 100644 <h> <h> src/app.cs\0"
        var stdout = Bytes(
            "1 .M N... 100644 100644 100644 1111111111111111111111111111111111111111 " +
            "1111111111111111111111111111111111111111 src/app.cs" + NUL);

        var files = StatusPorcelainV2Parser.Parse(stdout);

        files.Count.ShouldBe(1);
        files[0].Kind.ShouldBe(ChangeKind.Modified);
        files[0].Path.ShouldBe("src/app.cs");
    }

    [Fact]
    public void Parse_OrdinaryAdded_XYHasA_MapsToAdded()
    {
        var stdout = Bytes(
            "1 A. N... 000000 100644 100644 0000000000000000000000000000000000000000 " +
            "2222222222222222222222222222222222222222 added.cs" + NUL);

        var files = StatusPorcelainV2Parser.Parse(stdout);

        files[0].Kind.ShouldBe(ChangeKind.Added);
        files[0].Path.ShouldBe("added.cs");
    }

    [Fact]
    public void Parse_OrdinaryDeleted_XYHasD_MapsToDeleted()
    {
        var stdout = Bytes(
            "1 .D N... 100644 100644 000000 3333333333333333333333333333333333333333 " +
            "3333333333333333333333333333333333333333 removed.cs" + NUL);

        var files = StatusPorcelainV2Parser.Parse(stdout);

        files[0].Kind.ShouldBe(ChangeKind.Deleted);
        files[0].Path.ShouldBe("removed.cs");
    }

    [Fact]
    public void Parse_Renamed_TypeTwoLine_SetsNewAndOldPath()
    {
        // Type 2: "2 R. N... <m> <m> <m> <h> <h> R100 new.cs\0old.cs\0"
        var stdout = Bytes(
            "2 R. N... 100644 100644 100644 4444444444444444444444444444444444444444 " +
            "4444444444444444444444444444444444444444 R100 new.cs" + NUL + "old.cs" + NUL);

        var files = StatusPorcelainV2Parser.Parse(stdout);

        files.Count.ShouldBe(1);
        files[0].Kind.ShouldBe(ChangeKind.Renamed);
        files[0].Path.ShouldBe("new.cs");
        files[0].OldPath.ShouldBe("old.cs");
    }

    [Fact]
    public void Parse_Copied_TypeTwoLineWithCPrefix_MapsToCopied()
    {
        // The type-2 XY field starting with 'C' distinguishes a copy from a rename; a
        // regression here would mislabel copies. (Previously untested branch.)
        var stdout = Bytes(
            "2 C. N... 100644 100644 100644 4444444444444444444444444444444444444444 " +
            "4444444444444444444444444444444444444444 C100 copy.cs" + NUL + "orig.cs" + NUL);

        var files = StatusPorcelainV2Parser.Parse(stdout);

        files.Count.ShouldBe(1);
        files[0].Kind.ShouldBe(ChangeKind.Copied);
        files[0].Path.ShouldBe("copy.cs");
        files[0].OldPath.ShouldBe("orig.cs");
    }

    [Fact]
    public void Parse_OrdinaryModified_PathWithSpaces_KeepsFullPath()
    {
        // The fixed-count split must keep a space-containing path intact at the final index.
        var stdout = Bytes(
            "1 .M N... 100644 100644 100644 1111111111111111111111111111111111111111 " +
            "1111111111111111111111111111111111111111 my source file.cs" + NUL);

        var files = StatusPorcelainV2Parser.Parse(stdout);

        files[0].Kind.ShouldBe(ChangeKind.Modified);
        files[0].Path.ShouldBe("my source file.cs");
    }

    [Fact]
    public void Parse_Renamed_NewPathWithSpaces_KeepsFullPath()
    {
        var stdout = Bytes(
            "2 R. N... 100644 100644 100644 4444444444444444444444444444444444444444 " +
            "4444444444444444444444444444444444444444 R100 my new name.cs" + NUL + "old.cs" + NUL);

        var files = StatusPorcelainV2Parser.Parse(stdout);

        files[0].Kind.ShouldBe(ChangeKind.Renamed);
        files[0].Path.ShouldBe("my new name.cs");
        files[0].OldPath.ShouldBe("old.cs");
    }

    [Fact]
    public void Parse_Unmerged_TypeULine_MapsToConflicted()
    {
        // Type u: "u UU N... <m1> <m2> <m3> <mW> <h1> <h2> <h3> conflicted.cs\0"
        var stdout = Bytes(
            "u UU N... 100644 100644 100644 100644 " +
            "5555555555555555555555555555555555555555 " +
            "6666666666666666666666666666666666666666 " +
            "7777777777777777777777777777777777777777 conflicted.cs" + NUL);

        var files = StatusPorcelainV2Parser.Parse(stdout);

        files.Count.ShouldBe(1);
        files[0].Kind.ShouldBe(ChangeKind.Conflicted);
        files[0].Path.ShouldBe("conflicted.cs");
    }

    [Fact]
    public void Parse_Unmerged_PathWithSpaces_KeepsFullPath()
    {
        // The conflicted path itself contains spaces; the fixed-count split must keep it whole.
        var stdout = Bytes(
            "u UU N... 100644 100644 100644 100644 " +
            "5555555555555555555555555555555555555555 " +
            "6666666666666666666666666666666666666666 " +
            "7777777777777777777777777777777777777777 my conflicted file.cs" + NUL);

        var files = StatusPorcelainV2Parser.Parse(stdout);

        files.Count.ShouldBe(1);
        files[0].Kind.ShouldBe(ChangeKind.Conflicted);
        files[0].Path.ShouldBe("my conflicted file.cs");
    }

    [Fact]
    public void Parse_IgnoredEntries_AreSkipped()
    {
        var stdout = Bytes("! ignored.log" + NUL + "? real.txt" + NUL);

        var files = StatusPorcelainV2Parser.Parse(stdout);

        files.Count.ShouldBe(1);
        files[0].Path.ShouldBe("real.txt");
    }

    [Fact]
    public void Parse_MixedAllLineTypes_ParsedInOrder()
    {
        var stdout = Bytes(
            "1 .M N... 100644 100644 100644 aaaa aaaa changed.cs" + NUL +
            "? untracked.txt" + NUL +
            "2 R. N... 100644 100644 100644 bbbb bbbb R100 dst.cs" + NUL + "srcname.cs" + NUL +
            "u UU N... 100644 100644 100644 100644 c1 c2 c3 conflict.cs" + NUL);

        var files = StatusPorcelainV2Parser.Parse(stdout);

        files.Count.ShouldBe(4);
        files[0].Kind.ShouldBe(ChangeKind.Modified);
        files[0].Path.ShouldBe("changed.cs");
        files[1].Kind.ShouldBe(ChangeKind.Untracked);
        files[1].Path.ShouldBe("untracked.txt");
        files[2].Kind.ShouldBe(ChangeKind.Renamed);
        files[2].Path.ShouldBe("dst.cs");
        files[2].OldPath.ShouldBe("srcname.cs");
        files[3].Kind.ShouldBe(ChangeKind.Conflicted);
        files[3].Path.ShouldBe("conflict.cs");
    }

    [Fact]
    public void Parse_UntrackedUnicodePath_DecodedAsUtf8()
    {
        var stdout = Bytes("? café/notes-é.md" + NUL);

        StatusPorcelainV2Parser.Parse(stdout)[0].Path.ShouldBe("café/notes-é.md");
    }

    [Fact]
    public void Parse_EmptyOutput_ReturnsEmptyList()
    {
        StatusPorcelainV2Parser.Parse([]).ShouldBeEmpty();
    }
}
