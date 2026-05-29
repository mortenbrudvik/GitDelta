using GitDelta.Core.Cli;
using GitDelta.Core.Git;
using GitDelta.Core.Models;
using GitDelta.Core.Settings;
using GitDelta.UI.Services;
using GitDelta.UI.ViewModels;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.ViewModels;

public class MainWindowViewModelTests
{
    private readonly IGitReader _git = Substitute.For<IGitReader>();
    private readonly ISettingsStore _settings = Substitute.For<ISettingsStore>();
    private readonly IFolderPicker _picker = Substitute.For<IFolderPicker>();
    private readonly IThemeService _theme = Substitute.For<IThemeService>();

    public MainWindowViewModelTests()
    {
        _settings.Load().Returns(new AppSettings());
        _git.CheckGitAsync(Arg.Any<CancellationToken>())
            .Returns(new GitAvailability(true, "2.40.1", true));
        _git.GetHistoryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CommitInfo>());
        _git.GetChangedFilesAsync(Arg.Any<string>(), Arg.Any<DiffSpec>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ChangedFile>());
    }

    private MainWindowViewModel Create() =>
        new(
            _git,
            () => new StartViewModel(_picker),
            () => new ShellViewModel(_git, _settings, _picker, _theme));

    [Fact]
    public async Task InitializeAsync_PrintHelp_ShowsStartScreen()
    {
        var sut = Create();

        await sut.InitializeAsync(new LaunchAction(LaunchActionKind.PrintHelp), CancellationToken.None);

        sut.CurrentContent.ShouldBeOfType<StartViewModel>();
    }

    [Fact]
    public async Task InitializeAsync_PrintVersion_ShowsStartScreen()
    {
        var sut = Create();

        await sut.InitializeAsync(new LaunchAction(LaunchActionKind.PrintVersion), CancellationToken.None);

        sut.CurrentContent.ShouldBeOfType<StartViewModel>();
    }

    [Fact]
    public async Task InitializeAsync_ShowStartScreen_ShowsStartScreen()
    {
        var sut = Create();

        await sut.InitializeAsync(new LaunchAction(LaunchActionKind.ShowStartScreen), CancellationToken.None);

        sut.CurrentContent.ShouldBeOfType<StartViewModel>();
    }

    [Fact]
    public async Task InitializeAsync_OpenRepoWorkingTree_WhenRepoFound_ShowsShell()
    {
        _git.FindRepositoryRootAsync(@"C:\some\path", Arg.Any<CancellationToken>())
            .Returns(@"C:\some\repo");
        var sut = Create();

        await sut.InitializeAsync(
            new LaunchAction(LaunchActionKind.OpenRepoWorkingTree, @"C:\some\path"),
            CancellationToken.None);

        var shell = sut.CurrentContent.ShouldBeOfType<ShellViewModel>();
        shell.RepoRoot.ShouldBe(@"C:\some\repo");
    }

    [Fact]
    public async Task InitializeAsync_OpenRepoWorkingTree_WhenNotARepo_FallsBackToStartScreen()
    {
        _git.FindRepositoryRootAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        var sut = Create();

        await sut.InitializeAsync(
            new LaunchAction(LaunchActionKind.OpenRepoWorkingTree, @"C:\not\a\repo"),
            CancellationToken.None);

        sut.CurrentContent.ShouldBeOfType<StartViewModel>();
    }

    [Fact]
    public async Task OpenRepositoryAsync_LoadsShellForGivenRoot()
    {
        var sut = Create();

        await sut.OpenRepositoryAsync(@"C:\repo");

        var shell = sut.CurrentContent.ShouldBeOfType<ShellViewModel>();
        shell.RepoRoot.ShouldBe(@"C:\repo");
    }

    [Fact]
    public async Task OpeningASecondRepository_DisposesThePreviousShell()
    {
        var sut = Create();
        await sut.OpenRepositoryAsync(@"C:\repo1");
        var firstShell = sut.CurrentContent.ShouldBeOfType<ShellViewModel>();

        await sut.OpenRepositoryAsync(@"C:\repo2");

        // The replaced shell was disposed, so it no longer reacts to theme changes.
        _theme.IsDarkChanged += Raise.Event<Action<bool>>(true);
        firstShell.Diff.IsDarkTheme.ShouldBeFalse();
        sut.CurrentContent.ShouldNotBeSameAs(firstShell);
    }

    [Fact]
    public async Task StartScreen_RepositorySelected_OpensThatRepository()
    {
        _picker.PickFolder(Arg.Any<string>()).Returns(@"C:\chosen\repo");
        _git.FindRepositoryRootAsync(@"C:\chosen\repo", Arg.Any<CancellationToken>())
            .Returns(@"C:\chosen\repo");
        var sut = Create();
        await sut.InitializeAsync(new LaunchAction(LaunchActionKind.ShowStartScreen), CancellationToken.None);
        var start = sut.CurrentContent.ShouldBeOfType<StartViewModel>();

        await start.OpenFolderCommand.ExecuteAsync(null);
        // Allow the async open kicked off by the event to settle.
        await Task.Yield();

        sut.CurrentContent.ShouldBeOfType<ShellViewModel>();
    }
}
