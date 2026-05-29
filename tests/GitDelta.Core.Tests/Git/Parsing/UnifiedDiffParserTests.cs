using GitDelta.Core.Git.Parsing;
using GitDelta.Core.Models;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Git.Parsing;

public class UnifiedDiffParserTests
{
    [Fact]
    public void Parse_SingleHunk_ContextAddedDeleted_AssignsLineNumbers()
    {
        var diff =
            "@@ -1,3 +1,4 @@\n" +
            " context one\n" +
            "-removed line\n" +
            "+added line A\n" +
            "+added line B\n" +
            " context two\n";

        var hunks = UnifiedDiffParser.Parse(diff);

        hunks.Count.ShouldBe(1);
        var h = hunks[0];
        h.OldStart.ShouldBe(1);
        h.OldCount.ShouldBe(3);
        h.NewStart.ShouldBe(1);
        h.NewCount.ShouldBe(4);
        h.Lines.Count.ShouldBe(5);

        // context one: old 1, new 1
        h.Lines[0].Kind.ShouldBe(DiffLineKind.Context);
        h.Lines[0].OldLineNumber.ShouldBe(1);
        h.Lines[0].NewLineNumber.ShouldBe(1);

        // removed: old 2, no new
        h.Lines[1].Kind.ShouldBe(DiffLineKind.Deleted);
        h.Lines[1].OldLineNumber.ShouldBe(2);
        h.Lines[1].NewLineNumber.ShouldBeNull();
        h.Lines[1].Text.ShouldBe("removed line");

        // added A: no old, new 2
        h.Lines[2].Kind.ShouldBe(DiffLineKind.Added);
        h.Lines[2].OldLineNumber.ShouldBeNull();
        h.Lines[2].NewLineNumber.ShouldBe(2);
        h.Lines[2].Text.ShouldBe("added line A");

        // added B: no old, new 3
        h.Lines[3].NewLineNumber.ShouldBe(3);

        // context two: old 3, new 4
        h.Lines[4].Kind.ShouldBe(DiffLineKind.Context);
        h.Lines[4].OldLineNumber.ShouldBe(3);
        h.Lines[4].NewLineNumber.ShouldBe(4);

        h.Lines.ShouldAllBe(l => l.IntraSpans.Count == 0);
    }

    [Fact]
    public void Parse_MultipleHunks_BothParsed()
    {
        var diff =
            "@@ -1,2 +1,2 @@\n" +
            "-old1\n" +
            "+new1\n" +
            " ctx\n" +
            "@@ -10,1 +10,2 @@ void Method()\n" +
            " keep\n" +
            "+inserted\n";

        var hunks = UnifiedDiffParser.Parse(diff);

        hunks.Count.ShouldBe(2);
        hunks[0].OldStart.ShouldBe(1);
        hunks[1].OldStart.ShouldBe(10);
        hunks[1].NewStart.ShouldBe(10);
        hunks[1].Header.ShouldBe("@@ -10,1 +10,2 @@ void Method()");
        hunks[1].Lines[0].Text.ShouldBe("keep");
        hunks[1].Lines[1].Kind.ShouldBe(DiffLineKind.Added);
        hunks[1].Lines[1].Text.ShouldBe("inserted");
    }

    [Fact]
    public void Parse_SingleLineHunkHeader_CountDefaultsToOne()
    {
        // "@@ -5 +5 @@" means old start 5 count 1, new start 5 count 1.
        var diff =
            "@@ -5 +5 @@\n" +
            "-x\n" +
            "+y\n";

        var hunks = UnifiedDiffParser.Parse(diff);

        hunks[0].OldStart.ShouldBe(5);
        hunks[0].OldCount.ShouldBe(1);
        hunks[0].NewStart.ShouldBe(5);
        hunks[0].NewCount.ShouldBe(1);
    }

    [Fact]
    public void Parse_NoNewlineMarker_DoesNotProduceLineOrAdvanceNumbers()
    {
        var diff =
            "@@ -1,1 +1,1 @@\n" +
            "-old last\n" +
            "\\ No newline at end of file\n" +
            "+new last\n" +
            "\\ No newline at end of file\n";

        var hunks = UnifiedDiffParser.Parse(diff);

        // Only the - and + content lines become DiffLines; markers are dropped.
        hunks[0].Lines.Count.ShouldBe(2);
        hunks[0].Lines[0].Kind.ShouldBe(DiffLineKind.Deleted);
        hunks[0].Lines[0].Text.ShouldBe("old last");
        hunks[0].Lines[1].Kind.ShouldBe(DiffLineKind.Added);
        hunks[0].Lines[1].Text.ShouldBe("new last");
    }

    [Fact]
    public void Parse_IgnoresFileHeaderLines_BeforeFirstHunk()
    {
        var diff =
            "diff --git a/f.cs b/f.cs\n" +
            "index 1234567..89abcde 100644\n" +
            "--- a/f.cs\n" +
            "+++ b/f.cs\n" +
            "@@ -1,1 +1,1 @@\n" +
            "-a\n" +
            "+b\n";

        var hunks = UnifiedDiffParser.Parse(diff);

        hunks.Count.ShouldBe(1);
        hunks[0].Lines.Count.ShouldBe(2);
        hunks[0].Lines[0].Text.ShouldBe("a");
        hunks[0].Lines[1].Text.ShouldBe("b");
    }

    [Fact]
    public void Parse_HunkHeaderWithNonNumericRange_SkipsHunkWithoutThrowing()
    {
        // A non-numeric range token (unexpected git format/locale) must degrade gracefully:
        // skip the malformed header rather than throw FormatException out of the parser.
        var diff =
            "@@ -x,3 +1,4 @@\n" +
            "+should be ignored\n" +
            "@@ -1,1 +1,1 @@\n" +
            "-a\n" +
            "+b\n";

        var hunks = UnifiedDiffParser.Parse(diff);

        hunks.Count.ShouldBe(1);
        hunks[0].OldStart.ShouldBe(1);
        hunks[0].Lines.Count.ShouldBe(2);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyList()
    {
        UnifiedDiffParser.Parse("").ShouldBeEmpty();
    }
}
