using GitDelta.Core.Git.Parsing;
using GitDelta.Core.Models;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Git.Parsing;

public class ChangedFileMergeTests
{
    [Fact]
    public void Merge_NameStatusKindAndNumstatCounts_CombinedByPath()
    {
        var numstat = new NumstatEntry[]
        {
            new("src/app.cs", null, 12, 3, false)
        };
        var nameStatus = new NameStatusEntry[]
        {
            new(ChangeKind.Modified, "src/app.cs", null)
        };

        var merged = ChangedFileMerge.Merge(numstat, nameStatus, []);

        merged.Count.ShouldBe(1);
        merged[0].Path.ShouldBe("src/app.cs");
        merged[0].Kind.ShouldBe(ChangeKind.Modified);
        merged[0].AddedLines.ShouldBe(12);
        merged[0].DeletedLines.ShouldBe(3);
        merged[0].IsBinary.ShouldBeFalse();
    }

    [Fact]
    public void Merge_Binary_FromNumstat_PropagatesIsBinaryAndNullCounts()
    {
        var numstat = new NumstatEntry[]
        {
            new("logo.png", null, null, null, true)
        };
        var nameStatus = new NameStatusEntry[]
        {
            new(ChangeKind.Added, "logo.png", null)
        };

        var merged = ChangedFileMerge.Merge(numstat, nameStatus, []);

        merged[0].IsBinary.ShouldBeTrue();
        merged[0].AddedLines.ShouldBeNull();
        merged[0].DeletedLines.ShouldBeNull();
        merged[0].Kind.ShouldBe(ChangeKind.Added);
    }

    [Fact]
    public void Merge_Rename_OldPathFromNameStatus_CountsFromNumstat()
    {
        var numstat = new NumstatEntry[]
        {
            new("new/name.cs", "old/name.cs", 1, 1, false)
        };
        var nameStatus = new NameStatusEntry[]
        {
            new(ChangeKind.Renamed, "new/name.cs", "old/name.cs")
        };

        var merged = ChangedFileMerge.Merge(numstat, nameStatus, []);

        merged.Count.ShouldBe(1);
        merged[0].Kind.ShouldBe(ChangeKind.Renamed);
        merged[0].Path.ShouldBe("new/name.cs");
        merged[0].OldPath.ShouldBe("old/name.cs");
        merged[0].AddedLines.ShouldBe(1);
        merged[0].DeletedLines.ShouldBe(1);
    }

    [Fact]
    public void Merge_ExtraUntracked_AppendedWhenNotInDiffSources()
    {
        var numstat = new NumstatEntry[]
        {
            new("tracked.cs", null, 5, 0, false)
        };
        var nameStatus = new NameStatusEntry[]
        {
            new(ChangeKind.Modified, "tracked.cs", null)
        };
        var extra = new ChangedFile[]
        {
            new("untracked.txt", null, ChangeKind.Untracked, null, null, false)
        };

        var merged = ChangedFileMerge.Merge(numstat, nameStatus, extra);

        merged.Count.ShouldBe(2);
        merged.ShouldContain(f => f.Path == "tracked.cs" && f.Kind == ChangeKind.Modified);
        merged.ShouldContain(f => f.Path == "untracked.txt" && f.Kind == ChangeKind.Untracked);
    }

    [Fact]
    public void Merge_ExtraConflicted_OverridesDiffKindForSamePath()
    {
        // A conflicted file may also show up in numstat/name-status; the porcelain
        // 'extra' conflicted status is authoritative and must win.
        var numstat = new NumstatEntry[]
        {
            new("merge.cs", null, 4, 4, false)
        };
        var nameStatus = new NameStatusEntry[]
        {
            new(ChangeKind.Modified, "merge.cs", null)
        };
        var extra = new ChangedFile[]
        {
            new("merge.cs", null, ChangeKind.Conflicted, null, null, false)
        };

        var merged = ChangedFileMerge.Merge(numstat, nameStatus, extra);

        merged.Count.ShouldBe(1);
        merged[0].Kind.ShouldBe(ChangeKind.Conflicted);
        // Counts from numstat are preserved even when the kind is overridden.
        merged[0].AddedLines.ShouldBe(4);
        merged[0].DeletedLines.ShouldBe(4);
    }

    [Fact]
    public void Merge_NumstatWithoutNameStatus_FallsBackToModified()
    {
        var numstat = new NumstatEntry[]
        {
            new("orphan.cs", null, 2, 0, false)
        };

        var merged = ChangedFileMerge.Merge(numstat, [], []);

        merged.Count.ShouldBe(1);
        merged[0].Path.ShouldBe("orphan.cs");
        merged[0].Kind.ShouldBe(ChangeKind.Modified);
        merged[0].AddedLines.ShouldBe(2);
    }

    [Fact]
    public void Merge_AllEmpty_ReturnsEmptyList()
    {
        ChangedFileMerge.Merge([], [], []).ShouldBeEmpty();
    }
}
