using GitDelta.Core.Models;

namespace GitDelta.UI.Controls.Diff;

/// <summary>One display line in a built diff document.</summary>
public sealed record DiffRow(
    DiffRowKind Kind,
    string Text,
    int? OldLineNumber,
    int? NewLineNumber,
    IReadOnlyList<IntraSpan> IntraSpans);

/// <summary>
/// A fully built side of a diff document: the joined text (one <see cref="DiffRow"/>
/// per line) plus the indexed per-line classification. Document line numbers are 1-based;
/// Rows[0] corresponds to AvalonEdit line 1.
/// </summary>
public sealed record DiffDocumentSide(string Text, IReadOnlyList<DiffRow> Rows);

/// <summary>
/// Result of building a document for a compare mode. For Unified, only
/// <see cref="Unified"/> is populated; for SideBySide, <see cref="Left"/> and
/// <see cref="Right"/> are populated and are guaranteed equal in row count.
/// </summary>
public sealed record DiffDocumentModel(
    DiffDocumentSide? Unified,
    DiffDocumentSide? Left,
    DiffDocumentSide? Right);
