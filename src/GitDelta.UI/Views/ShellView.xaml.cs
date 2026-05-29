using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using GitDelta.UI.ViewModels;

namespace GitDelta.UI.Views;

public partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private ShellViewModel? ViewModel => DataContext as ShellViewModel;

    // ── Pane-width persistence ──────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestorePaneWidths();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ShellViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnVmPropertyChanged;
        }

        if (e.NewValue is ShellViewModel newVm)
        {
            newVm.PropertyChanged += OnVmPropertyChanged;
        }

        RestorePaneWidths();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Intentionally empty — pane widths are pushed by code-behind to VM
        // via the GridSplitter DragCompleted handler, not the other way.
    }

    private void RestorePaneWidths()
    {
        var vm = ViewModel;
        if (vm is null || !IsLoaded)
        {
            return;
        }

        if (vm.HistoryPaneWidth >= 50)
        {
            HistoryColumn.Width = new GridLength(vm.HistoryPaneWidth);
        }

        if (vm.FilesPaneWidth >= 50)
        {
            FilesColumn.Width = new GridLength(vm.FilesPaneWidth);
        }
    }

    private void OnGridSplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        // Persist both widths atomically in a single Load+Save per drag.
        ViewModel?.PersistPaneWidths(HistoryColumn.Width.Value, FilesColumn.Width.Value);
    }

    // ── Changed-files grouping toggle ───────────────────────────────────────────

    private void OnGroupByFolderToggled(object sender, RoutedEventArgs e)
    {
        if (Resources["GroupedFiles"] is not CollectionViewSource cvs)
        {
            return;
        }

        cvs.GroupDescriptions.Clear();
        if (GroupByFolderToggle.IsChecked == true)
        {
            cvs.GroupDescriptions.Add(new PropertyGroupDescription("Folder"));
        }
    }

    // ── Toolbar click handlers (call DiffView public methods directly) ─────────

    private void OnFind(object sender, RoutedEventArgs e) => DiffControl.ShowFind();

    private void OnPreviousChange(object sender, RoutedEventArgs e) => DiffControl.GoToPreviousChange();

    private void OnNextChange(object sender, RoutedEventArgs e) => DiffControl.GoToNextChange();

    private void OnCopy(object sender, RoutedEventArgs e) => DiffControl.CopySelection();

    // ── History ListView: multi-select bridge → ShellViewModel ─────────────────

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        vm.SelectedCommits.Clear();
        foreach (var item in HistoryList.SelectedItems)
        {
            if (item is CommitRowViewModel row)
            {
                vm.SelectedCommits.Add(row);
            }
        }

        vm.OnHistorySelectionChanged();
    }

    // ── History ListView: infinite-scroll paging ────────────────────────────────

    private void HistoryList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Trigger paging when within ~3 rows of the bottom and there is content to scroll.
        if (e.VerticalOffset > 0
            && e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 3
            && ViewModel?.LoadMoreHistoryCommand.CanExecute(null) == true)
        {
            ViewModel.LoadMoreHistoryCommand.Execute(null);
        }
    }
}
