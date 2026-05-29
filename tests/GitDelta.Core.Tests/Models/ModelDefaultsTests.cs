using GitDelta.Core.Models;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Models;

public class ModelDefaultsTests
{
    [Fact]
    public void CommitInfo_TwoParents_IsMergeTrue_IsRootFalse()
    {
        var commit = new CommitInfo(
            Sha: "abc123def456",
            ShortSha: "abc123d",
            Parents: ["p1", "p2"],
            AuthorName: "Ann",
            AuthorEmail: "ann@example.com",
            AuthorDate: DateTimeOffset.UnixEpoch,
            CommitterName: "Ann",
            CommitterEmail: "ann@example.com",
            CommitDate: DateTimeOffset.UnixEpoch,
            Subject: "Merge",
            Body: "");

        commit.IsMerge.ShouldBeTrue();
        commit.IsRoot.ShouldBeFalse();
    }

    [Fact]
    public void CommitInfo_NoParents_IsRootTrue_IsMergeFalse()
    {
        var commit = new CommitInfo(
            Sha: "root",
            ShortSha: "root",
            Parents: [],
            AuthorName: "Ann",
            AuthorEmail: "ann@example.com",
            AuthorDate: DateTimeOffset.UnixEpoch,
            CommitterName: "Ann",
            CommitterEmail: "ann@example.com",
            CommitDate: DateTimeOffset.UnixEpoch,
            Subject: "Initial",
            Body: "");

        commit.IsRoot.ShouldBeTrue();
        commit.IsMerge.ShouldBeFalse();
    }

    [Fact]
    public void ChangedFile_Rename_CarriesOldPath()
    {
        var file = new ChangedFile(
            Path: "new/name.cs",
            OldPath: "old/name.cs",
            Kind: ChangeKind.Renamed,
            AddedLines: 2,
            DeletedLines: 1,
            IsBinary: false);

        file.OldPath.ShouldBe("old/name.cs");
        file.Kind.ShouldBe(ChangeKind.Renamed);
    }

    [Fact]
    public void DiffLine_DefaultsCanCarryEmptyIntraSpans()
    {
        var line = new DiffLine(
            Kind: DiffLineKind.Context,
            OldLineNumber: 1,
            NewLineNumber: 1,
            Text: "unchanged",
            IntraSpans: []);

        line.IntraSpans.ShouldBeEmpty();
    }

    [Fact]
    public void AppSettings_Defaults_MatchSpec()
    {
        var settings = new AppSettings();

        settings.Theme.ShouldBe(AppTheme.System);
        settings.DefaultDiffView.ShouldBe(DiffViewMode.SideBySide);
        settings.ContextLines.ShouldBe(3);
        settings.TabSize.ShouldBe(4);
        settings.SyntaxHighlighting.ShouldBeTrue();
        settings.ExternalEditorCommand.ShouldBeNull();
        settings.WindowWidth.ShouldBe(1100);
        settings.WindowHeight.ShouldBe(720);
        settings.HistoryPaneWidth.ShouldBe(280);
        settings.FilesPaneWidth.ShouldBe(260);
    }

    [Fact]
    public void AppSettings_WithExpression_OverridesSingleProperty()
    {
        var settings = new AppSettings { Theme = AppTheme.Dark };

        settings.Theme.ShouldBe(AppTheme.Dark);
        settings.TabSize.ShouldBe(4);
    }
}
