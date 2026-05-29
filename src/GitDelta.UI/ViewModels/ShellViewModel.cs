using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDelta.Core.Diff;
using GitDelta.Core.Git;
using GitDelta.Core.Models;
using GitDelta.Core.Settings;
using GitDelta.UI.Controls.Diff.Syntax;
using GitDelta.UI.Services;

namespace GitDelta.UI.ViewModels;

/// <summary>
/// The 3-pane workspace: history (left), changed files (middle), diff (right).
/// Owns the selection-to-DiffSpec recomputation and orchestrates IGitReader.
/// </summary>
public partial class ShellViewModel : ObservableObject, IDisposable
{
    private const int HistoryPageSize = 100;

    private readonly IGitReader _gitReader;
    private readonly IIntraLineDiffer _intraLineDiffer;
    private readonly ISettingsStore _settings;
    private readonly IFolderPicker _folderPicker;
    private readonly IThemeService _themeService;

    private AppSettings _appSettings;
    private int _historyLoaded;
    private int _busyCount;
    private bool _disposed;

    public ShellViewModel(
        IGitReader gitReader,
        IIntraLineDiffer intraLineDiffer,
        ISettingsStore settings,
        IFolderPicker folderPicker,
        IThemeService themeService)
    {
        _gitReader = gitReader;
        _intraLineDiffer = intraLineDiffer;
        _settings = settings;
        _folderPicker = folderPicker;
        _themeService = themeService;
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

        // ShellViewModel is transient; IThemeService is a singleton. Subscribe here
        // and unsubscribe in Dispose so a replaced shell can never leak via this
        // event (MainWindowViewModel.DetachCurrentContent disposes the old shell).
        _historyPaneWidth = _appSettings.HistoryPaneWidth;
        _filesPaneWidth = _appSettings.FilesPaneWidth;

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

    /// <summary>Unsubscribes from the singleton theme service to avoid a leak.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _themeService.IsDarkChanged -= OnThemeIsDarkChanged;
    }

    [ObservableProperty]
    private string? _repoRoot;

    [ObservableProperty]
    private string? _repoName;

    [ObservableProperty]
    private bool _isRepoLoaded;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

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
            _ = ShowFileDiffAsync(value, CancellationToken.None);
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
        if (--_busyCount == 0)
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
        await RecomputeComparisonAsync(ct);
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

            ChangedFiles.Clear();
            foreach (ChangedFile file in files)
            {
                ChangedFiles.Add(new FileRowViewModel(file));
            }

            SelectedFile = null;
            Diff.FileDiff = null;
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
    /// two commits => TwoCommits(older, newer). History is newest-first,
    /// so the higher index is the older (base) commit.
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

        if (SelectedCommits.Count == 2)
        {
            int indexA = History.IndexOf(SelectedCommits[0]);
            int indexB = History.IndexOf(SelectedCommits[1]);

            // Larger index = older = base; smaller index = newer = target.
            CommitRowViewModel older = indexA >= indexB ? SelectedCommits[0] : SelectedCommits[1];
            CommitRowViewModel newer = indexA >= indexB ? SelectedCommits[1] : SelectedCommits[0];

            return new DiffSpec.TwoCommits(older.Sha, newer.Sha);
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

        _ = RecomputeComparisonAsync(CancellationToken.None);
    }

    /// <summary>Raised when the user asks to open a different repository.</summary>
    public event Action<string>? RepositoryOpenRequested;

    /// <summary>Raised when the user opens the settings dialog.</summary>
    public event Action? SettingsRequested;

    /// <summary>Raised with the absolute path of the file to open externally.</summary>
    public event Action<string>? EditorRequested;

    /// <summary>
    /// Loads the per-file diff for the current comparison, enriches it with
    /// intra-line spans, and pushes it into the diff document.
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

            FileDiff enriched = IntraLineEnricher.Enrich(diff, _intraLineDiffer);

            Diff.SyntaxLanguageId = _appSettings.SyntaxHighlighting
                ? LanguageIdMap.FromPath(file.DisplayPath)
                : null;
            Diff.FileDiff = enriched;
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
        await RecomputeComparisonAsync(ct);
    }

    [RelayCommand]
    private async Task LoadMoreHistoryAsync(CancellationToken ct)
    {
        if (RepoRoot is null)
        {
            return;
        }

        IReadOnlyList<CommitInfo> page =
            await _gitReader.GetHistoryAsync(RepoRoot, _historyLoaded, HistoryPageSize, ct);
        foreach (CommitInfo commit in page)
        {
            History.Add(new CommitRowViewModel(commit));
        }

        _historyLoaded += page.Count;
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
        EditorRequested?.Invoke(absolute);
    }
}
