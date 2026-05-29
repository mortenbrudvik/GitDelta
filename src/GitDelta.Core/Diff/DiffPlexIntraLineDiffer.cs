using DiffPlex;
using DiffPlex.Chunkers;
using DiffPlex.Model;
using GitDelta.Core.Models;

namespace GitDelta.Core.Diff;

/// <summary>
/// Computes word-level intra-line change spans for a deleted/added line pair using DiffPlex.
/// Returned spans are character ranges into the respective input line.
/// </summary>
public sealed class DiffPlexIntraLineDiffer : IIntraLineDiffer
{
    private readonly IDiffer _differ = new Differ();

    public (IReadOnlyList<IntraSpan> Deleted, IReadOnlyList<IntraSpan> Added) Compute(
        string deletedLine, string addedLine)
    {
        if (deletedLine == addedLine)
        {
            return (Array.Empty<IntraSpan>(), Array.Empty<IntraSpan>());
        }

        // Word chunker => word-level granularity (matches the spec's "word-level" intra-line highlighting).
        // DiffPlex 1.9.0: CreateDiffs(oldText, newText, ignoreWhitespaceChanges, ignoreCase, chunker)
        DiffResult result = _differ.CreateDiffs(
            deletedLine, addedLine,
            false, false,
            new WordChunker());

        IReadOnlyList<string> pieces = result.PiecesOld;

        var deleted = new List<IntraSpan>();
        var added = new List<IntraSpan>();

        foreach (DiffBlock block in result.DiffBlocks)
        {
            if (block.DeleteCountA > 0)
            {
                AddSpan(deleted, pieces, block.DeleteStartA, block.DeleteCountA, IntraSpanKind.Deleted);
            }

            if (block.InsertCountB > 0)
            {
                AddSpan(added, result.PiecesNew, block.InsertStartB, block.InsertCountB, IntraSpanKind.Added);
            }
        }

        return (deleted, added);
    }

    private static void AddSpan(
        List<IntraSpan> spans, IReadOnlyList<string> pieces, int startPiece, int countPiece, IntraSpanKind kind)
    {
        // Convert a [startPiece, startPiece+countPiece) piece range into a character [start, length) range.
        int charStart = 0;
        for (int i = 0; i < startPiece; i++)
        {
            charStart += pieces[i].Length;
        }

        int charLength = 0;
        for (int i = startPiece; i < startPiece + countPiece; i++)
        {
            charLength += pieces[i].Length;
        }

        spans.Add(new IntraSpan(charStart, charLength, kind));
    }
}
