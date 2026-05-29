using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using GitDelta.Core.Cli;
using GitDelta.Core.Git;

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
    private readonly Func<StartViewModel> _startFactory;
    private readonly Func<ShellViewModel> _shellFactory;

    private StartViewModel? _currentStart;
    private ShellViewModel? _currentShell;

    public MainWindowViewModel(
        IGitReader gitReader,
        Func<StartViewModel> startFactory,
        Func<ShellViewModel> shellFactory)
    {
        _gitReader = gitReader;
        _startFactory = startFactory;
        _shellFactory = shellFactory;
    }

    [ObservableProperty]
    private ObservableObject? _currentContent;

    /// <summary>
    /// User-visible error surfaced when an asynchronous repository switch
    /// (kicked off by an event handler) fails, instead of crashing silently.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

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
        DetachCurrentContent();

        StartViewModel start = _startFactory();
        start.RepositorySelected += OnRepositorySelected;
        _currentStart = start;
        CurrentContent = start;
        return Task.CompletedTask;
    }

    /// <summary>Loads the repository at <paramref name="repoRoot"/> into the shell.</summary>
    public async Task OpenRepositoryAsync(string repoRoot)
    {
        DetachCurrentContent();

        ShellViewModel shell = _shellFactory();
        shell.RepositoryOpenRequested += OnShellRepositoryOpenRequested;
        _currentShell = shell;
        CurrentContent = shell;
        ErrorMessage = null;
        await shell.LoadRepositoryAsync(repoRoot, CancellationToken.None);
    }

    /// <summary>
    /// Unsubscribes the event handler of whichever child VM is currently shown
    /// so it can never fire after being replaced (avoids double-subscription).
    /// </summary>
    private void DetachCurrentContent()
    {
        if (_currentStart is not null)
        {
            _currentStart.RepositorySelected -= OnRepositorySelected;
            _currentStart = null;
        }

        if (_currentShell is not null)
        {
            _currentShell.RepositoryOpenRequested -= OnShellRepositoryOpenRequested;
            // ShellViewModel subscribes to the singleton IThemeService; disposing it
            // unsubscribes so the replaced shell cannot leak via that event.
            _currentShell.Dispose();
            _currentShell = null;
        }
    }

    private async void OnRepositorySelected(string folder)
    {
        await SwitchRepositoryAsync(folder);
    }

    private async void OnShellRepositoryOpenRequested(string folder)
    {
        await SwitchRepositoryAsync(folder);
    }

    /// <summary>
    /// Resolves the repository root for <paramref name="folder"/> and swaps in a
    /// fresh shell. Invoked from <c>async void</c> event handlers, so any failure
    /// is caught and surfaced via <see cref="ErrorMessage"/> rather than escaping
    /// to the global unhandled-exception handler.
    /// </summary>
    private async Task SwitchRepositoryAsync(string folder)
    {
        try
        {
            string? root = await _gitReader.FindRepositoryRootAsync(folder, CancellationToken.None);
            await OpenRepositoryAsync(root ?? folder);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not open '{folder}': {ex.Message}";
        }
    }
}
