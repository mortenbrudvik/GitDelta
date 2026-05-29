using GitDelta.Core.Diff;
using GitDelta.Core.Models;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Diff;

public class DiffPlexIntraLineDifferTests
{
    private readonly DiffPlexIntraLineDiffer _sut = new();

    [Fact]
    public void Compute_OneChangedWord_IsolatesThatWordOnBothSides()
    {
        var (deleted, added) = _sut.Compute("the quick brown fox", "the slow brown fox");

        // Deleted side: "quick" occupies chars [4, 5) length 5.
        deleted.Count.ShouldBe(1);
        deleted[0].Kind.ShouldBe(IntraSpanKind.Deleted);
        "the quick brown fox".Substring(deleted[0].Start, deleted[0].Length).ShouldBe("quick");

        // Added side: "slow" occupies chars [4, 4) length 4.
        added.Count.ShouldBe(1);
        added[0].Kind.ShouldBe(IntraSpanKind.Added);
        "the slow brown fox".Substring(added[0].Start, added[0].Length).ShouldBe("slow");
    }

    [Fact]
    public void Compute_IdenticalLines_ReturnsNoSpans()
    {
        var (deleted, added) = _sut.Compute("same text here", "same text here");

        deleted.ShouldBeEmpty();
        added.ShouldBeEmpty();
    }

    [Fact]
    public void Compute_PureInsertionAtEnd_ReportsOnlyAddedSpan()
    {
        var (deleted, added) = _sut.Compute("int x = 1", "int x = 1; // note");

        deleted.ShouldBeEmpty();
        added.Count.ShouldBeGreaterThan(0);
        added.ShouldAllBe(s => s.Kind == IntraSpanKind.Added);
        // The added text must include the appended fragment.
        var addedText = string.Concat(added.Select(s => "int x = 1; // note".Substring(s.Start, s.Length)));
        addedText.ShouldContain("// note");
    }

    [Fact]
    public void Compute_PureDeletion_ReportsOnlyDeletedSpan()
    {
        var (deleted, added) = _sut.Compute("int x = 1; // note", "int x = 1");

        added.ShouldBeEmpty();
        deleted.Count.ShouldBeGreaterThan(0);
        deleted.ShouldAllBe(s => s.Kind == IntraSpanKind.Deleted);
    }

    [Fact]
    public void Compute_SpansAreWithinLineBounds()
    {
        const string del = "alpha beta gamma";
        const string add = "alpha BETA gamma";

        var (deleted, added) = _sut.Compute(del, add);

        foreach (var s in deleted)
        {
            s.Start.ShouldBeGreaterThanOrEqualTo(0);
            (s.Start + s.Length).ShouldBeLessThanOrEqualTo(del.Length);
        }
        foreach (var s in added)
        {
            s.Start.ShouldBeGreaterThanOrEqualTo(0);
            (s.Start + s.Length).ShouldBeLessThanOrEqualTo(add.Length);
        }
    }
}
