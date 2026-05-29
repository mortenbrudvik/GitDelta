using CommunityToolkit.Mvvm.ComponentModel;
using GitDelta.Core.Models;

namespace GitDelta.UI.ViewModels;

/// <summary>
/// One changed file in the middle pane for the current comparison.
/// </summary>
public partial class FileRowViewModel : ObservableObject
{
    public FileRowViewModel(ChangedFile file)
    {
        File = file;
        DisplayPath = file.Path;
        Kind = file.Kind;
        Added = file.AddedLines;
        Deleted = file.DeletedLines;
        IsBinary = file.IsBinary;
        StatusGlyph = MapGlyph(file.Kind);

        int slash = file.Path.LastIndexOfAny(['/', '\\']);
        if (slash < 0)
        {
            Folder = string.Empty;
            FileName = file.Path;
        }
        else
        {
            Folder = file.Path[..slash];
            FileName = file.Path[(slash + 1)..];
        }
    }

    public ChangedFile File { get; }

    public string DisplayPath { get; }
    public string Folder { get; }
    public string FileName { get; }
    public ChangeKind Kind { get; }
    public int? Added { get; }
    public int? Deleted { get; }
    public bool IsBinary { get; }
    public string StatusGlyph { get; }

    private static string MapGlyph(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => "A",
        ChangeKind.Modified => "M",
        ChangeKind.Deleted => "D",
        ChangeKind.Renamed => "R",
        ChangeKind.Copied => "C",
        ChangeKind.TypeChanged => "T",
        ChangeKind.Untracked => "U",
        ChangeKind.Conflicted => "!",
        _ => "?"
    };
}
