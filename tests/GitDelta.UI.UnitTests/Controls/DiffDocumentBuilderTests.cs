using GitDelta.Core.Models;
using GitDelta.UI.Controls.Diff;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.Controls;

public sealed class DiffDocumentBuilderTests
{
    private static FileDiff MakeDiff(params DiffLine[] lines)
    {
        var hunk = new DiffHunk(1, 0, 1, 0, "@@ -1 +1 @@", lines);
        var file = new ChangedFile("a.cs", null, ChangeKind.Modified, 1, 1, false);
        return new FileDiff(file, new[] { hunk }, IsBinary: false, IsTruncated: false);
    }

    private static DiffLine Ctx(string t, int oldN, int newN) =>
        new(DiffLineKind.Context, oldN, newN, t, Array.Empty<IntraSpan>());

    private static DiffLine Del(string t, int oldN) =>
        new(DiffLineKind.Deleted, oldN, null, t, Array.Empty<IntraSpan>());

    private static DiffLine Add(string t, int newN) =>
        new(DiffLineKind.Added, null, newN, t, Array.Empty<IntraSpan>());

    [Fact]
    public void SideBySide_aligns_unequal_runs_with_filler_rows()
    {
        // one deletion, two additions => right side gets the extra add,
        // left side gets a filler to stay aligned.
        FileDiff diff = MakeDiff(
            Ctx("ctx", 1, 1),
            Del("gone", 2),
            Add("new1", 2),
            Add("new2", 3));

        DiffDocumentModel model = DiffDocumentBuilder.Build(diff, DiffViewMode.SideBySide);

        model.Left.ShouldNotBeNull();
        model.Right.ShouldNotBeNull();
        model.Left!.Rows.Count.ShouldBe(model.Right!.Rows.Count);

        // Row 0 = context on both sides.
        model.Left.Rows[0].Kind.ShouldBe(DiffRowKind.Context);
        model.Right.Rows[0].Kind.ShouldBe(DiffRowKind.Context);

        // Row 1: left deleted "gone", right modified "new1".
        model.Left.Rows[1].Kind.ShouldBe(DiffRowKind.Deleted);
        model.Left.Rows[1].Text.ShouldBe("gone");
        model.Right.Rows[1].Kind.ShouldBe(DiffRowKind.Modified);
        model.Right.Rows[1].Text.ShouldBe("new1");

        // Row 2: left filler, right added "new2".
        model.Left.Rows[2].Kind.ShouldBe(DiffRowKind.Filler);
        model.Left.Rows[2].Text.ShouldBe(string.Empty);
        model.Right.Rows[2].Kind.ShouldBe(DiffRowKind.Added);
        model.Right.Rows[2].Text.ShouldBe("new2");
    }

    [Fact]
    public void SideBySide_text_has_one_line_per_row()
    {
        FileDiff diff = MakeDiff(Ctx("a", 1, 1), Del("b", 2), Add("c", 2));
        DiffDocumentModel model = DiffDocumentBuilder.Build(diff, DiffViewMode.SideBySide);

        model.Left!.Text.Split('\n').Length.ShouldBe(model.Left.Rows.Count);
        model.Right!.Text.Split('\n').Length.ShouldBe(model.Right.Rows.Count);
    }

    [Fact]
    public void Unified_inlines_deletes_then_adds_in_order()
    {
        FileDiff diff = MakeDiff(Ctx("a", 1, 1), Del("b", 2), Add("c", 2));
        DiffDocumentModel model = DiffDocumentBuilder.Build(diff, DiffViewMode.Unified);

        model.Unified.ShouldNotBeNull();
        model.Left.ShouldBeNull();
        model.Right.ShouldBeNull();

        model.Unified!.Rows.Select(r => r.Kind).ShouldBe(new[]
        {
            DiffRowKind.Context, DiffRowKind.Deleted, DiffRowKind.Added,
        });
        model.Unified.Rows[1].Text.ShouldBe("b");
        model.Unified.Rows[2].Text.ShouldBe("c");
    }

    [Fact]
    public void Modified_pair_keeps_intra_spans_on_both_sides()
    {
        var delSpans = new[] { new IntraSpan(0, 4, IntraSpanKind.Deleted) };
        var addSpans = new[] { new IntraSpan(0, 3, IntraSpanKind.Added) };
        FileDiff diff = MakeDiff(
            new DiffLine(DiffLineKind.Deleted, 1, null, "gone", delSpans),
            new DiffLine(DiffLineKind.Added, null, 1, "new", addSpans));

        DiffDocumentModel model = DiffDocumentBuilder.Build(diff, DiffViewMode.SideBySide);

        model.Left!.Rows[0].IntraSpans.ShouldBe(delSpans);
        model.Right!.Rows[0].IntraSpans.ShouldBe(addSpans);
    }
}
