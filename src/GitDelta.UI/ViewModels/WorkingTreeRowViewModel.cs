using CommunityToolkit.Mvvm.ComponentModel;

namespace GitDelta.UI.ViewModels;

/// <summary>
/// The pinned top row of the history list: "Working tree (uncommitted)".
/// Selecting it compares the working tree against HEAD.
/// </summary>
public partial class WorkingTreeRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _hasChanges;

    public string Summary => "Uncommitted changes";
}
