using GitDelta.Core.Models;

namespace GitDelta.Core.Diff;

/// <summary>
/// Fills <see cref="DiffLine.IntraSpans"/> by pairing equal-length consecutive runs of
/// Deleted lines immediately followed by Added lines (1:1) and running the supplied differ.
/// Returns a new immutable <see cref="FileDiff"/>; the input is not mutated.
/// </summary>
public static class IntraLineEnricher
{
    public static FileDiff Enrich(FileDiff diff, IIntraLineDiffer differ)
    {
        var newHunks = new List<DiffHunk>(diff.Hunks.Count);

        foreach (DiffHunk hunk in diff.Hunks)
        {
            newHunks.Add(EnrichHunk(hunk, differ));
        }

        return diff with { Hunks = newHunks };
    }

    private static DiffHunk EnrichHunk(DiffHunk hunk, IIntraLineDiffer differ)
    {
        var lines = hunk.Lines;
        var result = new DiffLine[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            result[i] = lines[i];
        }

        int index = 0;
        while (index < lines.Count)
        {
            if (lines[index].Kind != DiffLineKind.Deleted)
            {
                index++;
                continue;
            }

            // Collect the maximal run of consecutive Deleted lines.
            int deleteStart = index;
            while (index < lines.Count && lines[index].Kind == DiffLineKind.Deleted)
            {
                index++;
            }
            int deleteCount = index - deleteStart;

            // Collect the maximal run of Added lines immediately following.
            int addStart = index;
            while (index < lines.Count && lines[index].Kind == DiffLineKind.Added)
            {
                index++;
            }
            int addCount = index - addStart;

            // Pair 1:1 only when the runs are equal length and both non-empty.
            if (deleteCount > 0 && deleteCount == addCount)
            {
                for (int k = 0; k < deleteCount; k++)
                {
                    DiffLine del = lines[deleteStart + k];
                    DiffLine add = lines[addStart + k];

                    var (delSpans, addSpans) = differ.Compute(del.Text, add.Text);

                    result[deleteStart + k] = del with { IntraSpans = delSpans };
                    result[addStart + k] = add with { IntraSpans = addSpans };
                }
            }
        }

        return hunk with { Lines = result };
    }
}
