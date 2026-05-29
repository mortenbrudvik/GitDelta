using GitDelta.Core.Models;

namespace GitDelta.Core.Git.Parsing;

public sealed record NameStatusEntry(ChangeKind Kind, string Path, string? OldPath);
