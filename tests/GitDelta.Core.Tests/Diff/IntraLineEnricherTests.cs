using GitDelta.Core.Diff;
using GitDelta.Core.Models;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Diff;

public class IntraLineEnricherTests
{
    // Deterministic stub: marks the whole deleted line and whole added line as one span each.
    private sealed class WholeLineDiffer : IIntraLineDiffer
    {
        public (IReadOnlyList<IntraSpan> Deleted, IReadOnlyList<IntraSpan> Added) Compute(
            string deletedLine, string addedLine) =>
            (
                new[] { new IntraSpan(0, deletedLine.Length, IntraSpanKind.Deleted) },
                new[] { new IntraSpan(0, addedLine.Length, IntraSpanKind.Added) }
            );
    }

    private static DiffLine Line(DiffLineKind kind, string text) =>
        new(kind, null, null, text, Array.Empty<IntraSpan>());

    private static FileDiff DiffWith(params DiffLine[] lines)
    {
        var file = new ChangedFile("a.txt", null, ChangeKind.Modified, 1, 1, IsBinary: false);
        var hunk = new DiffHunk(1, lines.Length, 1, lines.Length, "@@ -1 +1 @@", lines);
        return new FileDiff(file, new[] { hunk }, IsBinary: false, IsTruncated: false);
    }

    [Fact]
    public void Enrich_EqualLengthDeleteThenAddRun_PairsAndFillsSpans()
    {
        var diff = DiffWith(
            Line(DiffLineKind.Deleted, "old one"),
            Line(DiffLineKind.Deleted, "old two"),
            Line(DiffLineKind.Added, "new one"),
            Line(DiffLineKind.Added, "new two"));

        var result = IntraLineEnricher.Enrich(diff, new WholeLineDiffer());

        var enriched = result.Hunks[0].Lines;
        enriched[0].IntraSpans.Count.ShouldBe(1); // deleted "old one"
        enriched[0].IntraSpans[0].Kind.ShouldBe(IntraSpanKind.Deleted);
        enriched[0].IntraSpans[0].Length.ShouldBe("old one".Length);
        enriched[1].IntraSpans.Count.ShouldBe(1); // deleted "old two"
        enriched[2].IntraSpans.Count.ShouldBe(1); // added "new one"
        enriched[2].IntraSpans[0].Kind.ShouldBe(IntraSpanKind.Added);
        enriched[3].IntraSpans.Count.ShouldBe(1); // added "new two"
    }

    [Fact]
    public void Enrich_UnequalRunLengths_LeavesSpansEmpty()
    {
        var diff = DiffWith(
            Line(DiffLineKind.Deleted, "old one"),
            Line(DiffLineKind.Added, "new one"),
            Line(DiffLineKind.Added, "new two"));

        var result = IntraLineEnricher.Enrich(diff, new WholeLineDiffer());

        foreach (var line in result.Hunks[0].Lines)
        {
            line.IntraSpans.ShouldBeEmpty();
        }
    }

    [Fact]
    public void Enrich_ContextLinesAreNeverTouched()
    {
        var diff = DiffWith(
            Line(DiffLineKind.Context, "unchanged"),
            Line(DiffLineKind.Deleted, "old one"),
            Line(DiffLineKind.Added, "new one"),
            Line(DiffLineKind.Context, "also unchanged"));

        var result = IntraLineEnricher.Enrich(diff, new WholeLineDiffer());

        var lines = result.Hunks[0].Lines;
        lines[0].IntraSpans.ShouldBeEmpty();         // leading context
        lines[1].IntraSpans.Count.ShouldBe(1);       // deleted paired
        lines[2].IntraSpans.Count.ShouldBe(1);       // added paired
        lines[3].IntraSpans.ShouldBeEmpty();         // trailing context
    }

    [Fact]
    public void Enrich_AddsWithNoPrecedingDelete_LeavesSpansEmpty()
    {
        var diff = DiffWith(
            Line(DiffLineKind.Context, "ctx"),
            Line(DiffLineKind.Added, "brand new"));

        var result = IntraLineEnricher.Enrich(diff, new WholeLineDiffer());

        foreach (var line in result.Hunks[0].Lines)
        {
            line.IntraSpans.ShouldBeEmpty();
        }
    }

    [Fact]
    public void Enrich_PreservesFileAndHunkMetadata()
    {
        var diff = DiffWith(
            Line(DiffLineKind.Deleted, "x"),
            Line(DiffLineKind.Added, "y"));

        var result = IntraLineEnricher.Enrich(diff, new WholeLineDiffer());

        result.File.Path.ShouldBe("a.txt");
        result.IsBinary.ShouldBeFalse();
        result.IsTruncated.ShouldBeFalse();
        result.Hunks[0].Header.ShouldBe("@@ -1 +1 @@");
        result.Hunks[0].OldStart.ShouldBe(1);
    }
}
