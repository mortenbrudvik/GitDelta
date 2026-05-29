using GitDelta.Core.Models;

namespace GitDelta.Core.Git.Parsing;

/// <summary>
/// Combines the changed-file sources into the final ChangedFile list. name-status
/// supplies the authoritative ChangeKind and old/new paths; numstat supplies the
/// +/- counts and the binary flag; 'extra' (from status porcelain v2) contributes
/// untracked/conflicted files. Entries are keyed by new path. An 'extra' entry for
/// an already-present path overrides the kind (e.g. Conflicted wins) while keeping
/// the numstat counts; an 'extra' entry for a new path is appended.
/// </summary>
public static class ChangedFileMerge
{
    public static IReadOnlyList<ChangedFile> Merge(
        IReadOnlyList<NumstatEntry> numstat,
        IReadOnlyList<NameStatusEntry> nameStatus,
        IReadOnlyList<ChangedFile> extra)
    {
        var nameStatusByPath = new Dictionary<string, NameStatusEntry>(StringComparer.Ordinal);
        foreach (var ns in nameStatus)
        {
            nameStatusByPath[ns.Path] = ns;
        }

        var ordered = new List<ChangedFile>();
        var indexByPath = new Dictionary<string, int>(StringComparer.Ordinal);

        // 1. numstat drives the primary list (it has counts + binary flag).
        foreach (var entry in numstat)
        {
            ChangeKind kind;
            string? oldPath;
            if (nameStatusByPath.TryGetValue(entry.Path, out var ns))
            {
                kind = ns.Kind;
                oldPath = ns.OldPath ?? entry.OldPath;
            }
            else
            {
                kind = ChangeKind.Modified;
                oldPath = entry.OldPath;
            }

            indexByPath[entry.Path] = ordered.Count;
            ordered.Add(new ChangedFile(entry.Path, oldPath, kind, entry.Added, entry.Deleted, entry.IsBinary));
        }

        // 2. name-status entries with no numstat counterpart (rare; defensive).
        foreach (var ns in nameStatus)
        {
            if (!indexByPath.ContainsKey(ns.Path))
            {
                indexByPath[ns.Path] = ordered.Count;
                ordered.Add(new ChangedFile(ns.Path, ns.OldPath, ns.Kind, null, null, false));
            }
        }

        // 3. extra (untracked/conflicted): override kind for existing paths,
        //    append new paths.
        foreach (var ex in extra)
        {
            if (indexByPath.TryGetValue(ex.Path, out var existingIndex))
            {
                var existing = ordered[existingIndex];
                ordered[existingIndex] = existing with { Kind = ex.Kind };
            }
            else
            {
                indexByPath[ex.Path] = ordered.Count;
                ordered.Add(ex);
            }
        }

        return ordered;
    }
}
