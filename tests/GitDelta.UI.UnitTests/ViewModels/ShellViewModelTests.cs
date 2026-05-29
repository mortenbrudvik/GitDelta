using GitDelta.Core.Git;
using GitDelta.Core.Models;
using GitDelta.Core.Settings;
using GitDelta.UI.Services;
using GitDelta.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.ViewModels;

public class ShellViewModelTests
{
    private readonly IGitReader _git = Substitute.For<IGitReader>();
    private readonly ISettingsStore _settings = Substitute.For<ISettingsStore>();
    private readonly IFolderPicker _picker = Substitute.For<IFolderPicker>();
    private readonly IThemeService _theme = Substitute.For<IThemeService>();
    private readonly IExternalEditorLauncher _editorLauncher = Substitute.For<IExternalEditorLauncher>();

    public ShellViewModelTests()
    {
        _settings.Load().Returns(new AppSettings());
        _git.CheckGitAsync(Arg.Any<CancellationToken>())
            .Returns(new GitAvailability(true, "2.40.1", true));
        _git.GetHistoryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CommitInfo>());
        _git.GetChangedFilesAsync(Arg.Any<string>(), Arg.Any<DiffSpec>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ChangedFile>());
    }

    private ShellViewModel Create() =>
        new(_git, _settings, _picker, _theme, _editorLauncher, NullLogger<ShellViewModel>.Instance);

    private static CommitInfo Commit(string sha, params string[] parents) =>
        new(sha, sha[..Math.Min(7, sha.Length)], parents,
            "A", "a@x", DateTimeOffset.UnixEpoch,
            "A", "a@x", DateTimeOffset.UnixEpoch, "subject", "body");

    [Fact]
    public void NewInstance_IsNotRepoLoaded()
    {
        var sut = Create();

        sut.IsRepoLoaded.ShouldBeFalse();
        sut.History.ShouldBeEmpty();
        sut.ChangedFiles.ShouldBeEmpty();
        sut.Diff.ShouldNotBeNull();
        sut.WorkingTreeRow.ShouldNotBeNull();
    }

    [Fact]
    public async Task LoadRepositoryAsync_SetsRepoRootAndName_AndMarksLoaded()
    {
        var sut = Create();

        await sut.LoadRepositoryAsync(@"C:\code\projects\GitDelta", CancellationToken.None);

        sut.RepoRoot.ShouldBe(@"C:\code\projects\GitDelta");
        sut.RepoName.ShouldBe("GitDelta");
        sut.IsRepoLoaded.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadRepositoryAsync_PopulatesHistoryFromGitReader()
    {
        _git.GetHistoryAsync(Arg.Any<string>(), Arg.Is<int>(n => n == 0), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { Commit("aaaaaaa1"), Commit("bbbbbbb2") });
        var sut = Create();

        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);

        sut.History.Count.ShouldBe(2);
        sut.History[0].Sha.ShouldBe("aaaaaaa1");
    }

    [Fact]
    public async Task LoadRepositoryAsync_DefaultsToWorkingTreeComparison()
    {
        var sut = Create();

        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);

        sut.WorkingTreeRow.IsSelected.ShouldBeTrue();
        // Verify WorkingTreeVsHead spec was used
        var wtCalls = _git.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IGitReader.GetChangedFilesAsync))
            .ToList();
        wtCalls.ShouldNotBeEmpty();
        var wtSpec = (DiffSpec)wtCalls.Last().GetArguments()[1]!;
        wtSpec.ShouldBeOfType<DiffSpec.WorkingTreeVsHead>();
    }

    [Fact]
    public async Task LoadRepositoryAsync_WhenGitMissing_SetsStatusMessage_AndDoesNotLoad()
    {
        _git.CheckGitAsync(Arg.Any<CancellationToken>())
            .Returns(new GitAvailability(false, null, false));
        var sut = Create();

        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);

        sut.IsRepoLoaded.ShouldBeFalse();
        sut.IsBusy.ShouldBeFalse();
        sut.StatusMessage.ShouldNotBeNullOrWhiteSpace();
        await _git.DidNotReceive().GetHistoryAsync(
            Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadRepositoryAsync_WhenGitVersionTooOld_SetsStatusMessage_AndDoesNotLoad()
    {
        _git.CheckGitAsync(Arg.Any<CancellationToken>())
            .Returns(new GitAvailability(true, "2.10.0", false));
        var sut = Create();

        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);

        sut.IsRepoLoaded.ShouldBeFalse();
        sut.StatusMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SelectingOneCommit_UsesCommitVsParentSpec()
    {
        _git.GetHistoryAsync(Arg.Any<string>(), Arg.Is<int>(n => n == 0), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { Commit("newer11", "older22"), Commit("older22") });
        var sut = Create();
        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);

        await sut.SelectCommitAsync(sut.History[0], CancellationToken.None);

        // Verify a CommitVsParent spec with sha "newer11" was used.
        var calls = _git.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IGitReader.GetChangedFilesAsync))
            .ToList();
        calls.ShouldNotBeEmpty();
        var lastSpec = (DiffSpec)calls.Last().GetArguments()[1]!;
        lastSpec.ShouldBeOfType<DiffSpec.CommitVsParent>()
            .Sha.ShouldBe("newer11");
        sut.WorkingTreeRow.IsSelected.ShouldBeFalse();
    }

    [Fact]
    public async Task SelectingTwoCommits_UsesTwoCommitsSpecOrderedOlderToNewer()
    {
        // History is newest-first: index 0 = newest.
        _git.GetHistoryAsync(Arg.Any<string>(), Arg.Is<int>(n => n == 0), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                Commit("newest1", "mid2222"),
                Commit("mid2222", "oldest3"),
                Commit("oldest3")
            });
        var sut = Create();
        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);

        // Select newest (index 0) then oldest (index 2): base must be oldest3, target newest1.
        await sut.SelectCommitAsync(sut.History[0], CancellationToken.None);
        await sut.SelectCommitAsync(sut.History[2], CancellationToken.None);

        // Verify TwoCommits spec ordered older->newer
        var twoCommitsCalls = _git.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IGitReader.GetChangedFilesAsync))
            .ToList();
        twoCommitsCalls.ShouldNotBeEmpty();
        var twoCommitsSpec = (DiffSpec)twoCommitsCalls.Last().GetArguments()[1]!;
        var tc = twoCommitsSpec.ShouldBeOfType<DiffSpec.TwoCommits>();
        tc.BaseSha.ShouldBe("oldest3");
        tc.TargetSha.ShouldBe("newest1");
    }

    [Fact]
    public async Task SelectingThirdCommit_ReplacesOldestSelection()
    {
        _git.GetHistoryAsync(Arg.Any<string>(), Arg.Is<int>(n => n == 0), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                Commit("c1aaaaa", "c2aaaaa"),
                Commit("c2aaaaa", "c3aaaaa"),
                Commit("c3aaaaa")
            });
        var sut = Create();
        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);

        await sut.SelectCommitAsync(sut.History[0], CancellationToken.None); // c1
        await sut.SelectCommitAsync(sut.History[1], CancellationToken.None); // c2
        await sut.SelectCommitAsync(sut.History[2], CancellationToken.None); // c3 (third click)

        // Never more than two selected.
        sut.SelectedCommits.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SelectingMoreThanTwoCommits_DiffsTheExtremesOldestToNewest()
    {
        // The history ListView's multi-select can populate SelectedCommits with more than
        // two commits; the range should diff the oldest (base) vs newest (target).
        _git.GetHistoryAsync(Arg.Any<string>(), Arg.Is<int>(n => n == 0), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                Commit("newest1", "second2"),
                Commit("second2", "third33"),
                Commit("third33", "oldest4"),
                Commit("oldest4")
            });
        var sut = Create();
        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);

        // Multi-select indices 0 (newest), 2, 3 (oldest) — added directly as the code-behind
        // bridge does, then drive the recompute.
        sut.SelectedCommits.Add(sut.History[0]); // newest1
        sut.SelectedCommits.Add(sut.History[2]); // third33
        sut.SelectedCommits.Add(sut.History[3]); // oldest4
        sut.OnHistorySelectionChanged();

        var calls = _git.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IGitReader.GetChangedFilesAsync))
            .ToList();
        calls.ShouldNotBeEmpty();
        var spec = (DiffSpec)calls.Last().GetArguments()[1]!;
        var tc = spec.ShouldBeOfType<DiffSpec.TwoCommits>();
        tc.BaseSha.ShouldBe("oldest4");   // highest History index = oldest = base
        tc.TargetSha.ShouldBe("newest1"); // lowest History index = newest = target
    }

    [Fact]
    public async Task SetComparisonAsync_PopulatesChangedFiles()
    {
        _git.GetChangedFilesAsync(@"C:\repo",
                Arg.Is<DiffSpec>(s => s is DiffSpec.TwoCommits),
                Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new ChangedFile("a.cs", null, ChangeKind.Modified, 1, 0, false),
                new ChangedFile("b.cs", null, ChangeKind.Added, 5, 0, false)
            });
        var sut = Create();
        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);

        await sut.SetComparisonAsync(new DiffSpec.TwoCommits("x", "y"), CancellationToken.None);

        sut.ChangedFiles.Count.ShouldBe(2);
        sut.ChangedFiles[0].DisplayPath.ShouldBe("a.cs");
    }

    [Fact]
    public async Task ShowFileDiffAsync_PushesReaderFileDiffIntoDiffDocument()
    {
        // IGitReader.GetFileDiffAsync already returns an enriched FileDiff; the shell
        // pushes it through as-is (no second enrichment pass).
        var changed = new ChangedFile("a.cs", null, ChangeKind.Modified, 1, 1, false);
        var hunk = new DiffHunk(1, 1, 1, 1, "@@ -1 +1 @@", new[]
        {
            new DiffLine(DiffLineKind.Deleted, 1, null, "old", []),
            new DiffLine(DiffLineKind.Added, null, 1, "new", [])
        });
        var fileDiff = new FileDiff(changed, new[] { hunk }, false, false);
        _git.GetFileDiffAsync(@"C:\repo", Arg.Any<DiffSpec>(), "a.cs",
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(fileDiff);
        var sut = Create();
        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);

        var fileRow = new FileRowViewModel(changed);
        await sut.ShowFileDiffAsync(fileRow, CancellationToken.None);

        sut.SelectedFile.ShouldBeSameAs(fileRow);
        sut.Diff.FileDiff.ShouldBeSameAs(fileDiff);
        sut.Diff.FileDiff!.File.Path.ShouldBe("a.cs");
        await _git.Received().GetFileDiffAsync(@"C:\repo", Arg.Any<DiffSpec>(), "a.cs",
            Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadRepositoryAsync_WhenHistoryLoadFails_SurfacesErrorAndClearsBusy()
    {
        // A failed 'git log' must not leave a blank workspace with no explanation.
        _git.GetHistoryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GitCommandException("log", 128, "fatal: bad revision"));
        var sut = Create();

        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);

        sut.StatusMessage.ShouldNotBeNull().ShouldContain("fatal: bad revision");
        sut.HasStatusMessage.ShouldBeTrue();
        sut.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshCommand_WhenGitCommandFails_SurfacesErrorInStatusMessage()
    {
        var sut = Create();
        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);
        _git.GetChangedFilesAsync(Arg.Any<string>(), Arg.Any<DiffSpec>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GitCommandException("diff --numstat", 128, "fatal: dubious ownership"));

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.StatusMessage.ShouldNotBeNull().ShouldContain("dubious ownership");
        sut.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task ChangingSelectedFile_CancelsThePreviousFileDiffLoad()
    {
        // Rapid file switching must cancel the in-flight load so a slower earlier read cannot
        // overwrite the diff for the file the user is now looking at.
        var sut = Create();
        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);

        var tokens = new List<CancellationToken>();
        var gate = new TaskCompletionSource<FileDiff>();
        _git.GetFileDiffAsync(Arg.Any<string>(), Arg.Any<DiffSpec>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                tokens.Add(ci.ArgAt<CancellationToken>(4));
                return gate.Task; // never completes: keeps the load "in flight"
            });

        sut.SelectedFile = new FileRowViewModel(new ChangedFile("a.cs", null, ChangeKind.Modified, 1, 0, false));
        sut.SelectedFile = new FileRowViewModel(new ChangedFile("b.cs", null, ChangeKind.Modified, 1, 0, false));

        tokens.Count.ShouldBe(2);
        tokens[0].IsCancellationRequested.ShouldBeTrue();  // first (superseded) load was cancelled
        tokens[1].IsCancellationRequested.ShouldBeFalse(); // current load is live
    }

    [Fact]
    public void Dispose_CancelsInFlightFileDiffLoad()
    {
        var sut = Create();

        CancellationToken captured = default;
        var gate = new TaskCompletionSource<FileDiff>();
        _git.GetFileDiffAsync(Arg.Any<string>(), Arg.Any<DiffSpec>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => { captured = ci.ArgAt<CancellationToken>(4); return gate.Task; });

        // Drive a load without the early RepoRoot guard returning: load a repo first.
        sut.RepoRoot = @"C:\repo";
        sut.WorkingTreeRow.IsSelected = true;
        sut.SelectedFile = new FileRowViewModel(new ChangedFile("a.cs", null, ChangeKind.Modified, 1, 0, false));

        sut.Dispose();

        captured.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadMoreHistoryCommand_AppendsNextPage()
    {
        _git.GetHistoryAsync(Arg.Any<string>(), Arg.Is<int>(n => n == 0), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { Commit("p1aaaaa"), Commit("p2aaaaa") });
        _git.GetHistoryAsync(Arg.Any<string>(), Arg.Is<int>(n => n == 2), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { Commit("p3aaaaa") });
        var sut = Create();
        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);

        await sut.LoadMoreHistoryCommand.ExecuteAsync(null);

        sut.History.Count.ShouldBe(3);
        sut.History[2].Sha.ShouldBe("p3aaaaa");
    }

    [Fact]
    public async Task RefreshCommand_ReloadsChangedFilesForCurrentSelection()
    {
        var sut = Create();
        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);
        _git.ClearReceivedCalls();
        _git.GetChangedFilesAsync(Arg.Any<string>(), Arg.Any<DiffSpec>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new ChangedFile("z.cs", null, ChangeKind.Added, 2, 0, false) });

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.ChangedFiles.Count.ShouldBe(1);
        sut.ChangedFiles[0].DisplayPath.ShouldBe("z.cs");
    }

    [Fact]
    public async Task OpenFolderCommand_WhenFolderChosen_RaisesRepositoryOpenRequested()
    {
        _picker.PickFolder(Arg.Any<string>()).Returns(@"C:\other\repo");
        var sut = Create();
        string? requested = null;
        sut.RepositoryOpenRequested += path => requested = path;

        await sut.OpenFolderCommand.ExecuteAsync(null);

        requested.ShouldBe(@"C:\other\repo");
    }

    [Fact]
    public async Task OpenFolderCommand_WhenCancelled_DoesNotRaiseEvent()
    {
        _picker.PickFolder(Arg.Any<string>()).Returns((string?)null);
        var sut = Create();
        var raised = false;
        sut.RepositoryOpenRequested += _ => raised = true;

        await sut.OpenFolderCommand.ExecuteAsync(null);

        raised.ShouldBeFalse();
    }

    [Fact]
    public void OpenSettingsCommand_RaisesSettingsRequested()
    {
        var sut = Create();
        var raised = false;
        sut.SettingsRequested += () => raised = true;

        sut.OpenSettingsCommand.Execute(null);

        raised.ShouldBeTrue();
    }

    [Fact]
    public async Task OpenFileInEditorCommand_WhenFileSelected_AsksLauncherToOpenAbsolutePath()
    {
        _editorLauncher.TryOpen(Arg.Any<string?>(), Arg.Any<string>()).Returns(true);
        var sut = Create();
        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);
        sut.SelectedFile = new FileRowViewModel(
            new ChangedFile("src/a.cs", null, ChangeKind.Modified, 1, 0, false));

        sut.OpenFileInEditorCommand.Execute(null);

        _editorLauncher.Received(1).TryOpen(Arg.Any<string?>(), @"C:\repo\src/a.cs");
    }

    [Fact]
    public void OpenFileInEditorCommand_WhenNoFileSelected_DoesNotCallLauncher()
    {
        var sut = Create();

        sut.OpenFileInEditorCommand.Execute(null);

        _editorLauncher.DidNotReceive().TryOpen(Arg.Any<string?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task OpenFileInEditorCommand_WhenLauncherFails_SurfacesStatusMessage()
    {
        _editorLauncher.TryOpen(Arg.Any<string?>(), Arg.Any<string>()).Returns(false);
        var sut = Create();
        await sut.LoadRepositoryAsync(@"C:\repo", CancellationToken.None);
        sut.SelectedFile = new FileRowViewModel(
            new ChangedFile("a.cs", null, ChangeKind.Modified, 1, 0, false));

        sut.OpenFileInEditorCommand.Execute(null);

        sut.HasStatusMessage.ShouldBeTrue();
    }

    [Fact]
    public void NewInstance_SeedsDiffIsDarkThemeFromThemeService()
    {
        _theme.IsDark.Returns(true);

        var sut = Create();

        sut.Diff.IsDarkTheme.ShouldBeTrue();
    }

    [Fact]
    public void WhenThemeServiceRaisesIsDarkChanged_DiffIsDarkThemeUpdates()
    {
        _theme.IsDark.Returns(false);
        var sut = Create();
        sut.Diff.IsDarkTheme.ShouldBeFalse();

        _theme.IsDarkChanged += Raise.Event<Action<bool>>(true);

        sut.Diff.IsDarkTheme.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_UnsubscribesFromThemeService_SoLaterChangesAreIgnored()
    {
        _theme.IsDark.Returns(false);
        var sut = Create();

        sut.Dispose();
        _theme.IsDarkChanged += Raise.Event<Action<bool>>(true);

        sut.Diff.IsDarkTheme.ShouldBeFalse();
    }

    [Fact]
    public void ToggleThemeCommand_CallsThemeServiceToggle()
    {
        var sut = Create();

        sut.ToggleThemeCommand.Execute(null);

        _theme.Received(1).Toggle();
    }
}
