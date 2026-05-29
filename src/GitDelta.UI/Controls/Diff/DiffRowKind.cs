namespace GitDelta.UI.Controls.Diff;

/// <summary>Per-display-line classification used by renderers/margin/colorizer.</summary>
public enum DiffRowKind
{
    Context,
    Added,
    Deleted,
    Modified,

    /// <summary>Imaginary/filler row that exists only to keep the two split sides aligned.</summary>
    Filler,
}
