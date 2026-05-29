namespace GitDelta.Core.Models;

/// <summary>WPF-free relative-time phrasing used by commit rows.</summary>
public static class RelativeTime
{
    public static string Format(DateTimeOffset when) => Format(when, DateTimeOffset.Now);

    public static string Format(DateTimeOffset when, DateTimeOffset now)
    {
        var delta = now - when;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        var seconds = delta.TotalSeconds;
        if (seconds < 60)
        {
            return "just now";
        }

        var minutes = (int)delta.TotalMinutes;
        if (minutes < 60)
        {
            return minutes == 1 ? "1 minute ago" : $"{minutes} minutes ago";
        }

        var hours = (int)delta.TotalHours;
        if (hours < 24)
        {
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        var days = (int)delta.TotalDays;
        if (days < 7)
        {
            return days == 1 ? "yesterday" : $"{days} days ago";
        }

        if (days < 30)
        {
            var weeks = days / 7;
            return weeks == 1 ? "last week" : $"{weeks} weeks ago";
        }

        if (days < 365)
        {
            var months = days / 30;
            return months == 1 ? "last month" : $"{months} months ago";
        }

        var years = days / 365;
        return years == 1 ? "last year" : $"{years} years ago";
    }
}
