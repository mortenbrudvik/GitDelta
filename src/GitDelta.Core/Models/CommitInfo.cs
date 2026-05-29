namespace GitDelta.Core.Models;

public sealed record CommitInfo(
    string Sha,
    string ShortSha,
    IReadOnlyList<string> Parents,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset AuthorDate,
    string CommitterName,
    string CommitterEmail,
    DateTimeOffset CommitDate,
    string Subject,
    string Body)
{
    public bool IsMerge => Parents.Count > 1;
    public bool IsRoot => Parents.Count == 0;
}
