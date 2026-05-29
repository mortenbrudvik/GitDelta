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

    public bool HasChanges { get; set; }

    public string Summary => "Uncommitted changes";
}
