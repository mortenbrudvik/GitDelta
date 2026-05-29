namespace GitDelta.Core.Models;

public enum ChangeKind
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,
    TypeChanged,
    Untracked,
    Conflicted
}

public sealed record ChangedFile(
    string Path,
    string? OldPath,
    ChangeKind Kind,
    int? AddedLines,
    int? DeletedLines,
    bool IsBinary);
