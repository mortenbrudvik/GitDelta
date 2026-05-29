using System.Windows;
using System.Windows.Controls;
using GitDelta.UI.ViewModels;

namespace GitDelta.UI.Views;

public partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();
    }

    private ShellViewModel? ViewModel => DataContext as ShellViewModel;

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
