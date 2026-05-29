using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using GitDelta.Core.Models;

namespace GitDelta.UI.ViewModels;

/// <summary>
/// One commit row in the left-hand history list.
/// </summary>
public partial class CommitRowViewModel : ObservableObject
{
    public CommitRowViewModel(CommitInfo commit)
    {
        Commit = commit;
        Sha = commit.Sha;
        ShortSha = commit.ShortSha;
        Subject = commit.Subject;
        Author = commit.AuthorName;
        FullDate = commit.AuthorDate.ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        RelativeDate = RelativeTime.Format(commit.AuthorDate);
    }

    public CommitInfo Commit { get; }

    public string Sha { get; }
    public string ShortSha { get; }
    public string Subject { get; }
    public string Author { get; }
    public string FullDate { get; }
    public string RelativeDate { get; }

    [ObservableProperty]
    private bool _isSelected;
}
