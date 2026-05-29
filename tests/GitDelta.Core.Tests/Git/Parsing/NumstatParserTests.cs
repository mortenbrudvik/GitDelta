using System.Text;
using GitDelta.Core.Git.Parsing;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Git.Parsing;

public class NumstatParserTests
{
    private const string TAB = "\t";
    private const string NUL = "\0";

    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Parse_NormalTextFile_ReadsAddedDeletedAndPath()
    {
        // "12\t3\tsrc/app.cs\0"
        var stdout = Bytes("12" + TAB + "3" + TAB + "src/app.cs" + NUL);

        var entries = NumstatParser.Parse(stdout);

        entries.Count.ShouldBe(1);
        entries[0].Added.ShouldBe(12);
        entries[0].Deleted.ShouldBe(3);
        entries[0].Path.ShouldBe("src/app.cs");
        entries[0].OldPath.ShouldBeNull();
        entries[0].IsBinary.ShouldBeFalse();
    }

    [Fact]
    public void Parse_BinaryFile_DashDashMeansNullCountsAndIsBinary()
    {
        // "-\t-\tassets/logo.png\0"
        var stdout = Bytes("-" + TAB + "-" + TAB + "assets/logo.png" + NUL);

        var entries = NumstatParser.Parse(stdout);

        entries.Count.ShouldBe(1);
        entries[0].IsBinary.ShouldBeTrue();
        entries[0].Added.ShouldBeNull();
        entries[0].Deleted.ShouldBeNull();
        entries[0].Path.ShouldBe("assets/logo.png");
    }

    [Fact]
    public void Parse_Rename_TwoPathNulRecord_SetsOldAndNewPath()
    {
        // Rename: counts, then NUL, then oldpath NUL newpath NUL
        // "4\t2\t\0old/name.cs\0new/name.cs\0"
        var stdout = Bytes("4" + TAB + "2" + TAB + NUL + "old/name.cs" + NUL + "new/name.cs" + NUL);

        var entries = NumstatParser.Parse(stdout);

        entries.Count.ShouldBe(1);
        entries[0].Added.ShouldBe(4);
        entries[0].Deleted.ShouldBe(2);
        entries[0].OldPath.ShouldBe("old/name.cs");
        entries[0].Path.ShouldBe("new/name.cs");
        entries[0].IsBinary.ShouldBeFalse();
    }

    [Fact]
    public void Parse_MultipleEntries_MixedNormalBinaryRename()
    {
        var stdout = Bytes(
            "10" + TAB + "0" + TAB + "added.txt" + NUL +
            "-" + TAB + "-" + TAB + "image.bin" + NUL +
            "1" + TAB + "1" + TAB + NUL + "a/old.cs" + NUL + "b/new.cs" + NUL);

        var entries = NumstatParser.Parse(stdout);

        entries.Count.ShouldBe(3);
        entries[0].Path.ShouldBe("added.txt");
        entries[0].Added.ShouldBe(10);
        entries[1].IsBinary.ShouldBeTrue();
        entries[1].Path.ShouldBe("image.bin");
        entries[2].OldPath.ShouldBe("a/old.cs");
        entries[2].Path.ShouldBe("b/new.cs");
    }

    [Fact]
    public void Parse_UnicodePath_DecodedAsUtf8_QuotepathOff()
    {
        // core.quotepath=false means raw UTF-8 bytes, no \xxx escaping.
        var stdout = Bytes("1" + TAB + "0" + TAB + "docs/café/notes-é.md" + NUL);

        var entries = NumstatParser.Parse(stdout);

        entries[0].Path.ShouldBe("docs/café/notes-é.md");
    }

    [Fact]
    public void Parse_NonNumericCounts_TreatedAsNullWithoutThrowing()
    {
        // A malformed count must not crash the parser; keep the entry with unknown counts.
        var stdout = Bytes("abc" + TAB + "5" + TAB + "src/app.cs" + NUL);

        var entries = NumstatParser.Parse(stdout);

        entries.Count.ShouldBe(1);
        entries[0].Path.ShouldBe("src/app.cs");
        entries[0].Added.ShouldBeNull();
        entries[0].Deleted.ShouldBe(5);
        entries[0].IsBinary.ShouldBeFalse();
    }

    [Fact]
    public void Parse_EmptyOutput_ReturnsEmptyList()
    {
        NumstatParser.Parse([]).ShouldBeEmpty();
    }
}
