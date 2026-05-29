using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using GitDelta.Core.Cli;
using GitDelta.Core.Git;
using GitDelta.Core.Settings;

namespace GitDelta.UI.ViewModels;

/// <summary>
/// Top-level content host. Routes the launch action to either the start
/// screen (<see cref="StartViewModel"/>) or the workspace
/// (<see cref="ShellViewModel"/>), and swaps content when the user opens a
/// different repository.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IGitReader _gitReader;
    private readonly ISettingsStore _settings;
    private readonly Func<StartViewModel> _startFactory;
    private readonly Func<ShellViewModel> _shellFactory;

    public MainWindowViewModel(
        IGitReader gitReader,
        ISettingsStore settings,
        Func<StartViewModel> startFactory,
        Func<ShellViewModel> shellFactory)
    {
        _gitReader = gitReader;
        _settings = settings;
        _startFactory = startFactory;
        _shellFactory = shellFactory;
    }

    [ObservableProperty]
    private ObservableObject? _currentContent;

    /// <summary>
    /// Routes the parsed launch action. PrintHelp/PrintVersion are handled by
    /// the bootstrap before the window appears, so here they fall through to
    /// the start screen. OpenRepoWorkingTree resolves the repo root; if it is
    /// not a repository, falls back to the start screen.
    /// </summary>
    public async Task InitializeAsync(LaunchAction action, CancellationToken ct)
    {
        switch (action.Kind)
        {
            case LaunchActionKind.OpenRepoWorkingTree:
                string start = action.RepoPath ?? Directory.GetCurrentDirectory();
                string? root = await _gitReader.FindRepositoryRootAsync(start, ct);
                if (root is null)
                {
                    await ShowStartAsync();
                }
                else
                {
                    await OpenRepositoryAsync(root);
                }
                break;

            case LaunchActionKind.ShowStartScreen:
            case LaunchActionKind.PrintHelp:
            case LaunchActionKind.PrintVersion:
            default:
                await ShowStartAsync();
                break;
        }
    }

    /// <summary>Shows the start screen and wires its repository-selected event.</summary>
    public Task ShowStartAsync()
    {
        StartViewModel start = _startFactory();
        start.RepositorySelected += OnRepositorySelected;
        CurrentContent = start;
        return Task.CompletedTask;
    }

    /// <summary>Loads the repository at <paramref name="repoRoot"/> into the shell.</summary>
    public async Task OpenRepositoryAsync(string repoRoot)
    {
        ShellViewModel shell = _shellFactory();
        shell.RepositoryOpenRequested += OnShellRepositoryOpenRequested;
        CurrentContent = shell;
        await shell.LoadRepositoryAsync(repoRoot, CancellationToken.None);
    }

    private async void OnRepositorySelected(string folder)
    {
        string? root = await _gitReader.FindRepositoryRootAsync(folder, CancellationToken.None);
        await OpenRepositoryAsync(root ?? folder);
    }

    private async void OnShellRepositoryOpenRequested(string folder)
    {
        string? root = await _gitReader.FindRepositoryRootAsync(folder, CancellationToken.None);
        await OpenRepositoryAsync(root ?? folder);
    }
}
