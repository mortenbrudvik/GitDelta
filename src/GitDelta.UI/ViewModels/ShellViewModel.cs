using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDelta.Core.Git;
using GitDelta.Core.Models;
using GitDelta.Core.Settings;
using GitDelta.UI.Controls.Diff.Syntax;
using GitDelta.UI.Services;
using Microsoft.Extensions.Logging;

namespace GitDelta.UI.ViewModels;

/// <summary>
/// The 3-pane workspace: history (left), changed files (middle), diff (right).
/// Owns the selection-to-DiffSpec recomputation and orchestrates IGitReader.
/// </summary>
public partial class ShellViewModel : ObservableObject, IDisposable
{
    private const int HistoryPageSize = 100;

    private readonly IGitReader _gitReader;
    private readonly ISettingsStore _settings;
    private readonly IFolderPicker _folderPicker;
    private readonly IThemeService _themeService;
    private readonly IExternalEditorLauncher _editorLauncher;
    private readonly ILogger<ShellViewModel> _logger;

    private AppSettings _appSettings;
    private int _historyLoaded;
    private int _busyCount;
    private bool _disposed;

    // Per-pane cancellation for the fire-and-forget loads kicked off by selection changes.
    // Each new selection cancels the previous in-flight load so results cannot arrive out of
    // order and overwrite the pane with stale content.
    private CancellationTokenSource? _fileDiffCts;
    private CancellationTokenSource? _comparisonCts;

    public ShellViewModel(
        IGitReader gitReader,
        ISettingsStore settings,
        IFolderPicker folderPicker,
        IThemeService themeService,
        IExternalEditorLauncher editorLauncher,
        ILogger<ShellViewModel> logger)
    {
        _gitReader = gitReader;
        _settings = settings;
        _folderPicker = folderPicker;
        _themeService = themeService;
        _editorLauncher = editorLauncher;
        _logger = logger;
        _appSettings = settings.Load();

        WorkingTreeRow = new WorkingTreeRowViewModel();
        Diff = new DiffDocumentViewModel
        {
            ViewMode = _appSettings.DefaultDiffView,
            TabSize = _appSettings.TabSize,
            // Track the effective application theme so the diff renderer picks the
            // matching DarkPlus/LightPlus syntax palette. Updated live below.
            IsDarkTheme = _themeService.IsDark
        };

        _historyPaneWidth = _appSettings.HistoryPaneWidth;
        _filesPaneWidth = _appSettings.FilesPaneWidth;

        // ShellViewModel is transient; IThemeService is a singleton. Subscribe here
        // and unsubscribe in Dispose so a replaced shell can never leak via this
        // event (MainWindowViewModel.DetachCurrentContent disposes the old shell).
        _themeService.IsDarkChanged += OnThemeIsDarkChanged;
    }

    private void OnThemeIsDarkChanged(bool isDark)
    {
        Diff.IsDarkTheme = isDark;
    }

    /// <summary>Toggles between light and dark and persists via the theme service.</summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.Toggle();
    }

    /// <summary>
    /// Unsubscribes from the singleton theme service to avoid a leak, and cancels/disposes
    /// any in-flight per-pane loads so they cannot complete against a disposed shell.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _themeService.IsDarkChanged -= OnThemeIsDarkChanged;

        CancelAndDispose(ref _fileDiffCts);
        CancelAndDispose(ref _comparisonCts);
    }

    [ObservableProperty]
    private string? _repoRoot;

    [ObservableProperty]
    private string? _repoName;

    [ObservableProperty]
    private bool _isRepoLoaded;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// A user-facing status/error line (git not installed, git too old, or a failed git
    /// command). Bound to an InfoBar in ShellView via <see cref="HasStatusMessage"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string? _statusMessage;

    /// <summary>True when <see cref="StatusMessage"/> has content; drives InfoBar visibility.</summary>
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    [ObservableProperty]
    private double _historyPaneWidth;

    [ObservableProperty]
    private double _filesPaneWidth;

    /// <summary>
    /// Persists both pane widths atomically in a single Load+Save. Called once per
    /// GridSplitter drag (not from the property setters) so one drag = one round-trip.
    /// </summary>
    public void PersistPaneWidths(double historyWidth, double filesWidth)
    {
        HistoryPaneWidth = historyWidth;
        FilesPaneWidth = filesWidth;

        var current = _settings.Load();
        _settings.Save(current with { HistoryPaneWidth = historyWidth, FilesPaneWidth = filesWidth });
    }

    public WorkingTreeRowViewModel WorkingTreeRow { get; }

    public ObservableCollection<CommitRowViewModel> History { get; } = [];

    public ObservableCollection<CommitRowViewModel> SelectedCommits { get; } = [];

    public ObservableCollection<FileRowViewModel> ChangedFiles { get; } = [];

    [ObservableProperty]
    private FileRowViewModel? _selectedFile;

    partial void OnSelectedFileChanged(FileRowViewModel? value)
    {
        if (value is not null)
        {
            // Cancel any prior file-diff load so a slower earlier read can't overwrite this one.
            CancellationToken token = ResetCts(ref _fileDiffCts);
            _ = RunGuardedAsync(ct => ShowFileDiffAsync(value, ct), token);
        }
    }

    public DiffDocumentViewModel Diff { get; }

    /// <summary>
    /// Loads a repository: validates git, sets RepoRoot/RepoName, loads the
    /// first history page, and shows the working-tree-vs-HEAD comparison.
    /// </summary>
    public async Task LoadRepositoryAsync(string repoRoot, CancellationToken ct)
    {
        using (BeginBusy())
        {
            GitAvailability availability = await _gitReader.CheckGitAsync(ct);
            if (!availability.IsInstalled)
            {
                StatusMessage = "Git was not found on your PATH. " +
                    "Install Git for Windows (2.30 or newer) and restart GitDelta.";
                IsRepoLoaded = false;
                return;
            }

            if (!availability.MeetsMinimum)
            {
                StatusMessage = $"Git {availability.Version} is too old. " +
                    "GitDelta needs Git 2.30 or newer.";
                IsRepoLoaded = false;
                return;
            }

            RepoRoot = repoRoot;
            RepoName = GetRepoName(repoRoot);

            try
            {
                History.Clear();
                SelectedCommits.Clear();
                _historyLoaded = 0;

                IReadOnlyList<CommitInfo> page =
                    await _gitReader.GetHistoryAsync(repoRoot, 0, HistoryPageSize, ct);
                foreach (CommitInfo commit in page)
                {
                    History.Add(new CommitRowViewModel(commit));
                }

                _historyLoaded = page.Count;
                IsRepoLoaded = true;
                StatusMessage = null;

                await SelectWorkingTreeAsync(ct);
            }
            catch (GitCommandException ex)
            {
                // A broken repo (bad object, dubious ownership, corrupt index, ...) must show
                // the failure, not an empty workspace that reads as "nothing here".
                _logger.LogWarning(ex, "Failed to load repository {RepoRoot}", repoRoot);
                StatusMessage = ex.Message;
            }
        }
    }

    /// <summary>
    /// Marks the view-model busy for the lifetime of the returned token.
    /// Uses a re-entrant counter so nested operations keep <see cref="IsBusy"/>
    /// true until the outermost one completes, and is robust to exceptions
    /// (the counter is decremented on dispose).
    /// </summary>
    private IDisposable BeginBusy()
    {
        if (++_busyCount == 1)
        {
            IsBusy = true;
        }

        return new BusyScope(this);
    }

    private void EndBusy()
    {
        // Clamp against underflow: a double-dispose or unexpected interleaving must never
        // drive the counter negative and wedge IsBusy permanently true.
        if (_busyCount > 0 && --_busyCount == 0)
        {
            IsBusy = false;
        }
    }

    private sealed class BusyScope(ShellViewModel owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.EndBusy();
        }
    }

    private static string GetRepoName(string repoRoot)
    {
        string trimmed = repoRoot.TrimEnd('/', '\\');
        string name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    [RelayCommand]
    private async Task SelectWorkingTreeAsync(CancellationToken ct)
    {
        WorkingTreeRow.IsSelected = true;
        foreach (CommitRowViewModel row in History)
        {
            row.IsSelected = false;
        }
        SelectedCommits.Clear();
        await RunGuardedAsync(RecomputeComparisonAsync, ct);
    }

    /// <summary>
    /// Toggles selection of a commit row and recomputes the comparison.
    /// At most two commits may be selected; selecting a third drops the
    /// oldest selection (FIFO).
    /// </summary>
    public async Task SelectCommitAsync(CommitRowViewModel commit, CancellationToken ct)
    {
        WorkingTreeRow.IsSelected = false;

        if (SelectedCommits.Contains(commit))
        {
            SelectedCommits.Remove(commit);
            commit.IsSelected = false;
        }
        else
        {
            SelectedCommits.Add(commit);
            commit.IsSelected = true;

            while (SelectedCommits.Count > 2)
            {
                CommitRowViewModel dropped = SelectedCommits[0];
                SelectedCommits.RemoveAt(0);
                dropped.IsSelected = false;
            }
        }

        await RecomputeComparisonAsync(ct);
    }

    /// <summary>
    /// Loads ChangedFiles for the given spec into the middle pane.
    /// </summary>
    public async Task SetComparisonAsync(DiffSpec spec, CancellationToken ct)
    {
        if (RepoRoot is null)
        {
            return;
        }

        using (BeginBusy())
        {
            IReadOnlyList<ChangedFile> files =
                await _gitReader.GetChangedFilesAsync(RepoRoot, spec, ct);

            // A newer selection may have superseded this load while git was running; bail
            // before mutating the pane so the visible file list always matches the latest
            // selection rather than whichever read happened to finish last.
            ct.ThrowIfCancellationRequested();

            ChangedFiles.Clear();
            foreach (ChangedFile file in files)
            {
                ChangedFiles.Add(new FileRowViewModel(file));
            }

            SelectedFile = null;
            Diff.FileDiff = null;
            StatusMessage = null;
        }
    }

    private async Task RecomputeComparisonAsync(CancellationToken ct)
    {
        DiffSpec? spec = BuildSpecFromSelection();
        if (spec is null)
        {
            // Nothing selected: clear the file list and the diff so no stale
            // content remains visible.
            ChangedFiles.Clear();
            SelectedFile = null;
            Diff.FileDiff = null;
            return;
        }

        await SetComparisonAsync(spec, ct);
    }

    /// <summary>
    /// Maps the current selection to a DiffSpec:
    /// working tree => WorkingTreeVsHead; one commit => CommitVsParent;
    /// two or more commits => TwoCommits over the extremes of the selection.
    /// History is newest-first, so the higher index is the older (base) commit;
    /// for a range selection (>2) we diff the oldest selected (base) against the
    /// newest selected (target) so the user sees a meaningful diff, not a blank.
    /// </summary>
    private DiffSpec? BuildSpecFromSelection()
    {
        if (WorkingTreeRow.IsSelected)
        {
            return new DiffSpec.WorkingTreeVsHead();
        }

        if (SelectedCommits.Count == 1)
        {
            return new DiffSpec.CommitVsParent(SelectedCommits[0].Sha);
        }

        if (SelectedCommits.Count >= 2)
        {
            // Find the extremes by History index: largest index = oldest = base,
            // smallest index = newest = target.
            CommitRowViewModel oldest = SelectedCommits[0];
            CommitRowViewModel newest = SelectedCommits[0];
            int oldestIndex = History.IndexOf(oldest);
            int newestIndex = oldestIndex;

            foreach (CommitRowViewModel commit in SelectedCommits)
            {
                int index = History.IndexOf(commit);
                if (index > oldestIndex)
                {
                    oldest = commit;
                    oldestIndex = index;
                }

                if (index < newestIndex)
                {
                    newest = commit;
                    newestIndex = index;
                }
            }

            return new DiffSpec.TwoCommits(oldest.Sha, newest.Sha);
        }

        return null;
    }

    /// <summary>
    /// Called by the ShellView code-behind when the history ListView's multi-select
    /// changes. Pushes the selection into <see cref="SelectedCommits"/> and
    /// recomputes the comparison. When commits are selected, the pinned working-tree
    /// row is deselected; when the selection is empty the comparison is recomputed
    /// (which clears the file list and diff unless the working-tree row is selected).
    /// </summary>
    public void OnHistorySelectionChanged()
    {
        if (SelectedCommits.Count > 0)
        {
            WorkingTreeRow.IsSelected = false;
        }

        // Cancel any prior comparison load; a rapid sequence of selection changes must not
        // leave the file list showing the result of a superseded selection.
        CancellationToken token = ResetCts(ref _comparisonCts);
        _ = RunGuardedAsync(RecomputeComparisonAsync, token);
    }

    /// <summary>Raised when the user asks to open a different repository.</summary>
    public event Action<string>? RepositoryOpenRequested;

    /// <summary>Raised when the user opens the settings dialog.</summary>
    public event Action? SettingsRequested;

    /// <summary>
    /// Loads the per-file diff for the current comparison (already enriched with
    /// intra-line spans by <see cref="IGitReader.GetFileDiffAsync"/>) and pushes it
    /// into the diff document.
    /// </summary>
    public async Task ShowFileDiffAsync(FileRowViewModel file, CancellationToken ct)
    {
        if (RepoRoot is null)
        {
            return;
        }

        DiffSpec? spec = BuildSpecFromSelection();
        if (spec is null)
        {
            return;
        }

        SelectedFile = file;
        using (BeginBusy())
        {
            FileDiff diff = await _gitReader.GetFileDiffAsync(
                RepoRoot, spec, file.DisplayPath, _appSettings.ContextLines, ct);

            // Superseded by a newer file selection while git was running — don't apply a
            // stale diff over the file the user is now looking at.
            ct.ThrowIfCancellationRequested();

            Diff.SyntaxLanguageId = _appSettings.SyntaxHighlighting
                ? LanguageIdMap.FromPath(file.DisplayPath)
                : null;
            Diff.FileDiff = diff;
            StatusMessage = null;
        }
    }

    [RelayCommand]
    private Task OpenFolderAsync()
    {
        string? folder = _folderPicker.PickFolder("Open a Git repository");
        if (!string.IsNullOrEmpty(folder))
        {
            RepositoryOpenRequested?.Invoke(folder);
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        await RunGuardedAsync(RecomputeComparisonAsync, ct);
    }

    [RelayCommand]
    private async Task LoadMoreHistoryAsync(CancellationToken ct)
    {
        if (RepoRoot is null)
        {
            return;
        }

        await RunGuardedAsync(async token =>
        {
            IReadOnlyList<CommitInfo> page =
                await _gitReader.GetHistoryAsync(RepoRoot, _historyLoaded, HistoryPageSize, token);
            foreach (CommitInfo commit in page)
            {
                History.Add(new CommitRowViewModel(commit));
            }

            _historyLoaded += page.Count;
        }, ct);
    }

    /// <summary>
    /// Runs an asynchronous git-backed operation, converting cancellation into a silent no-op
    /// (a newer request superseded it) and a <see cref="GitCommandException"/> into a logged,
    /// user-visible <see cref="StatusMessage"/> instead of an unobserved fault or a blank pane.
    /// </summary>
    private async Task RunGuardedAsync(Func<CancellationToken, Task> operation, CancellationToken ct)
    {
        try
        {
            await operation(ct);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer selection or torn down on dispose; nothing to surface.
        }
        catch (GitCommandException ex)
        {
            _logger.LogWarning(ex, "git command failed");
            StatusMessage = ex.Message;
        }
    }

    /// <summary>Cancels and disposes the previous token source, returning a fresh token.</summary>
    private static CancellationToken ResetCts(ref CancellationTokenSource? cts)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
        return cts.Token;
    }

    private static void CancelAndDispose(ref CancellationTokenSource? cts)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenFileInEditor()
    {
        if (SelectedFile is null || RepoRoot is null)
        {
            return;
        }

        string absolute = Path.Combine(RepoRoot, SelectedFile.DisplayPath);

        // Load the editor command fresh so a change made in the settings dialog is honored.
        string? template = _settings.Load().ExternalEditorCommand;
        if (!_editorLauncher.TryOpen(template, absolute))
        {
            StatusMessage = $"Couldn't open '{SelectedFile.DisplayPath}' in an external editor.";
        }
    }
}
