using GitDelta.Core.Models;
using GitDelta.UI.ViewModels;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.ViewModels;

public class DiffDocumentViewModelTests
{
    [Fact]
    public void Defaults_AreSideBySideAndOff()
    {
        var vm = new DiffDocumentViewModel();

        vm.FileDiff.ShouldBeNull();
        vm.ViewMode.ShouldBe(DiffViewMode.SideBySide);
        vm.ShowWhitespace.ShouldBeFalse();
        vm.WordWrap.ShouldBeFalse();
        vm.TabSize.ShouldBe(4);
        vm.SyntaxLanguageId.ShouldBeNull();
        vm.IsDarkTheme.ShouldBeFalse();
    }

    [Fact]
    public void ToggleViewModeCommand_FlipsBetweenSideBySideAndUnified()
    {
        var vm = new DiffDocumentViewModel();

        vm.ToggleViewModeCommand.Execute(null);
        vm.ViewMode.ShouldBe(DiffViewMode.Unified);

        vm.ToggleViewModeCommand.Execute(null);
        vm.ViewMode.ShouldBe(DiffViewMode.SideBySide);
    }

    [Fact]
    public void ToggleWhitespaceCommand_FlipsShowWhitespace()
    {
        var vm = new DiffDocumentViewModel();

        vm.ToggleWhitespaceCommand.Execute(null);
        vm.ShowWhitespace.ShouldBeTrue();

        vm.ToggleWhitespaceCommand.Execute(null);
        vm.ShowWhitespace.ShouldBeFalse();
    }

    [Fact]
    public void ToggleWordWrapCommand_FlipsWordWrap()
    {
        var vm = new DiffDocumentViewModel();

        vm.ToggleWordWrapCommand.Execute(null);
        vm.WordWrap.ShouldBeTrue();

        vm.ToggleWordWrapCommand.Execute(null);
        vm.WordWrap.ShouldBeFalse();
    }

    [Fact]
    public void FileDiff_RaisesPropertyChanged()
    {
        var vm = new DiffDocumentViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DiffDocumentViewModel.FileDiff))
                raised = true;
        };

        var changed = new ChangedFile("a.txt", null, ChangeKind.Modified, 1, 0, false);
        vm.FileDiff = new FileDiff(changed, [], false, false);

        raised.ShouldBeTrue();
    }
}
