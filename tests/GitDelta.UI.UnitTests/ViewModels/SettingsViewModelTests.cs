using GitDelta.Core.Models;
using GitDelta.UI.ViewModels;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.ViewModels;

public class SettingsViewModelTests
{
    private static AppSettings Sample() => new()
    {
        Theme = AppTheme.Dark,
        DefaultDiffView = DiffViewMode.Unified,
        ContextLines = 5,
        TabSize = 2,
        SyntaxHighlighting = false,
        ExternalEditorCommand = "code -g {file}"
    };

    [Fact]
    public void Constructor_MirrorsAppSettingsIntoBindableFields()
    {
        var vm = new SettingsViewModel(Sample());

        vm.Theme.ShouldBe(AppTheme.Dark);
        vm.DefaultDiffView.ShouldBe(DiffViewMode.Unified);
        vm.ContextLines.ShouldBe(5);
        vm.TabSize.ShouldBe(2);
        vm.SyntaxHighlighting.ShouldBeFalse();
        vm.ExternalEditorCommand.ShouldBe("code -g {file}");
    }

    [Fact]
    public void SaveCommand_ProducesResultReflectingEditedValues()
    {
        var vm = new SettingsViewModel(Sample());

        vm.Theme = AppTheme.Light;
        vm.ContextLines = 7;
        vm.TabSize = 8;
        vm.SyntaxHighlighting = true;
        vm.ExternalEditorCommand = "notepad {file}";

        vm.SaveCommand.Execute(null);

        AppSettings result = vm.Result;
        result.Theme.ShouldBe(AppTheme.Light);
        result.ContextLines.ShouldBe(7);
        result.TabSize.ShouldBe(8);
        result.SyntaxHighlighting.ShouldBeTrue();
        result.ExternalEditorCommand.ShouldBe("notepad {file}");
    }

    [Fact]
    public void SaveCommand_PreservesNonEditedSettingsFromOriginal()
    {
        var original = Sample() with { WindowWidth = 1500, HistoryPaneWidth = 333 };
        var vm = new SettingsViewModel(original);

        vm.SaveCommand.Execute(null);

        // Window/pane sizes are not edited in the dialog; they must survive.
        vm.Result.WindowWidth.ShouldBe(1500);
        vm.Result.HistoryPaneWidth.ShouldBe(333);
    }

    [Fact]
    public void CancelCommand_LeavesResultEqualToOriginalSettings()
    {
        var original = Sample();
        var vm = new SettingsViewModel(original);

        vm.Theme = AppTheme.Light;     // edit but cancel
        vm.ContextLines = 99;

        vm.CancelCommand.Execute(null);

        vm.Result.Theme.ShouldBe(AppTheme.Dark);
        vm.Result.ContextLines.ShouldBe(5);
    }

    [Fact]
    public void SaveCommand_SetsSavedRequestedTrue_AndCancelDoesNot()
    {
        var saved = new SettingsViewModel(Sample());
        var cancelled = new SettingsViewModel(Sample());

        saved.SaveCommand.Execute(null);
        cancelled.CancelCommand.Execute(null);

        saved.SaveRequested.ShouldBeTrue();
        cancelled.SaveRequested.ShouldBeFalse();
    }
}
