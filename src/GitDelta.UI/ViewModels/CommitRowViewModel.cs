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
        RelativeDate = FormatRelative(commit.AuthorDate);
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

    private static string FormatRelative(DateTimeOffset when)
    {
        TimeSpan delta = DateTimeOffset.Now - when;

        if (delta < TimeSpan.Zero)
            return "just now";
        if (delta.TotalSeconds < 60)
            return "just now";
        if (delta.TotalMinutes < 60)
        {
            int m = (int)delta.TotalMinutes;
            return m == 1 ? "1 minute ago" : $"{m} minutes ago";
        }
        if (delta.TotalHours < 24)
        {
            int h = (int)delta.TotalHours;
            return h == 1 ? "1 hour ago" : $"{h} hours ago";
        }
        if (delta.TotalDays < 30)
        {
            int d = (int)delta.TotalDays;
            return d == 1 ? "1 day ago" : $"{d} days ago";
        }
        if (delta.TotalDays < 365)
        {
            int mo = (int)(delta.TotalDays / 30);
            return mo == 1 ? "1 month ago" : $"{mo} months ago";
        }

        int y = (int)(delta.TotalDays / 365);
        return y == 1 ? "1 year ago" : $"{y} years ago";
    }
}
