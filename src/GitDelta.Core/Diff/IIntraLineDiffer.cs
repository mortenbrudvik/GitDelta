using GitDelta.Core.Models;

namespace GitDelta.Core.Diff;

/// <summary>
/// Computes character-range intra-line change spans for a deleted/added line pair.
/// </summary>
public interface IIntraLineDiffer
{
    (IReadOnlyList<IntraSpan> Deleted, IReadOnlyList<IntraSpan> Added) Compute(
        string deletedLine, string addedLine);
}
