namespace GitDelta.Core.Git.Parsing;

public sealed record NumstatEntry(string Path, string? OldPath, int? Added, int? Deleted, bool IsBinary);
