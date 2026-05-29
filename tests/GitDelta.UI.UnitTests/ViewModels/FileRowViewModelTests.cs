using GitDelta.Core.Models;
using GitDelta.UI.ViewModels;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.ViewModels;

public class FileRowViewModelTests
{
    private static ChangedFile File(
        string path,
        ChangeKind kind = ChangeKind.Modified,
        string? oldPath = null,
        int? added = 3,
        int? deleted = 1,
        bool isBinary = false) =>
        new(path, oldPath, kind, added, deleted, isBinary);

    [Fact]
    public void Constructor_CopiesCoreFields()
    {
        var file = File("src/app/Program.cs", ChangeKind.Modified, added: 10, deleted: 4);

        var vm = new FileRowViewModel(file);

        vm.File.ShouldBeSameAs(file);
        vm.DisplayPath.ShouldBe("src/app/Program.cs");
        vm.Kind.ShouldBe(ChangeKind.Modified);
        vm.Added.ShouldBe(10);
        vm.Deleted.ShouldBe(4);
        vm.IsBinary.ShouldBeFalse();
    }

    [Fact]
    public void FolderAndFileName_SplitOnLastForwardSlash()
    {
        var vm = new FileRowViewModel(File("src/app/Program.cs"));

        vm.Folder.ShouldBe("src/app");
        vm.FileName.ShouldBe("Program.cs");
    }

    [Fact]
    public void FolderAndFileName_SplitOnLastBackslash()
    {
        var vm = new FileRowViewModel(File("src\\app\\Program.cs"));

        vm.Folder.ShouldBe("src\\app");
        vm.FileName.ShouldBe("Program.cs");
    }

    [Fact]
    public void Folder_IsEmpty_WhenPathHasNoSeparator()
    {
        var vm = new FileRowViewModel(File("README.md"));

        vm.Folder.ShouldBeEmpty();
        vm.FileName.ShouldBe("README.md");
    }

    [Theory]
    [InlineData(ChangeKind.Added, "A")]
    [InlineData(ChangeKind.Modified, "M")]
    [InlineData(ChangeKind.Deleted, "D")]
    [InlineData(ChangeKind.Renamed, "R")]
    [InlineData(ChangeKind.Copied, "C")]
    [InlineData(ChangeKind.TypeChanged, "T")]
    [InlineData(ChangeKind.Untracked, "U")]
    [InlineData(ChangeKind.Conflicted, "!")]
    public void StatusGlyph_MapsEachChangeKind(ChangeKind kind, string expected)
    {
        var vm = new FileRowViewModel(File("a.txt", kind));

        vm.StatusGlyph.ShouldBe(expected);
    }

    [Fact]
    public void IsBinary_PropagatesFromChangedFile()
    {
        var vm = new FileRowViewModel(File("logo.png", ChangeKind.Added, isBinary: true, added: null, deleted: null));

        vm.IsBinary.ShouldBeTrue();
        vm.Added.ShouldBeNull();
        vm.Deleted.ShouldBeNull();
    }
}
