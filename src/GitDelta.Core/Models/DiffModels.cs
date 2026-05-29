namespace GitDelta.Core.Models;

public enum DiffLineKind
{
    Context,
    Added,
    Deleted
}

public enum IntraSpanKind
{
    Added,
    Deleted
}

public sealed record IntraSpan(int Start, int Length, IntraSpanKind Kind);

public sealed record DiffLine(
    DiffLineKind Kind,
    int? OldLineNumber,
    int? NewLineNumber,
    string Text,
    IReadOnlyList<IntraSpan> IntraSpans);

public sealed record DiffHunk(
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount,
    string Header,
    IReadOnlyList<DiffLine> Lines);

public sealed record FileDiff(
    ChangedFile File,
    IReadOnlyList<DiffHunk> Hunks,
    bool IsBinary,
    bool IsTruncated);
