using GitDelta.UI.Services;
using GitDelta.UI.ViewModels;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.ViewModels;

public class StartViewModelTests
{
    private readonly IFolderPicker _picker = Substitute.For<IFolderPicker>();

    [Fact]
    public void GitMissing_DefaultsToFalse()
    {
        var vm = new StartViewModel(_picker);

        vm.GitMissing.ShouldBeFalse();
    }

    [Fact]
    public void GitMissingMessage_IsNonEmpty()
    {
        var vm = new StartViewModel(_picker);

        vm.GitMissingMessage.ShouldNotBeNullOrWhiteSpace();
        vm.GitMissingMessage.ShouldContain("Git", Case.Insensitive);
    }

    [Fact]
    public async Task OpenFolderAsync_WhenFolderChosen_RaisesRepositorySelectedWithPath()
    {
        _picker.PickFolder(Arg.Any<string>()).Returns(@"C:\repos\demo");
        var vm = new StartViewModel(_picker);
        string? selected = null;
        vm.RepositorySelected += path => selected = path;

        await vm.OpenFolderCommand.ExecuteAsync(null);

        selected.ShouldBe(@"C:\repos\demo");
    }

    [Fact]
    public async Task OpenFolderAsync_WhenCancelled_DoesNotRaiseRepositorySelected()
    {
        _picker.PickFolder(Arg.Any<string>()).Returns((string?)null);
        var vm = new StartViewModel(_picker);
        var raised = false;
        vm.RepositorySelected += _ => raised = true;

        await vm.OpenFolderCommand.ExecuteAsync(null);

        raised.ShouldBeFalse();
    }
}
