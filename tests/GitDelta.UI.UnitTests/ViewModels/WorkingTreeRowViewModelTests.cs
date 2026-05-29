using GitDelta.UI.ViewModels;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.ViewModels;

public class WorkingTreeRowViewModelTests
{
    [Fact]
    public void IsSelected_DefaultsToFalse_AndIsSettable()
    {
        var vm = new WorkingTreeRowViewModel();

        vm.IsSelected.ShouldBeFalse();

        vm.IsSelected = true;

        vm.IsSelected.ShouldBeTrue();
    }

    [Fact]
    public void HasChanges_DefaultsToFalse_AndIsSettable()
    {
        var vm = new WorkingTreeRowViewModel();

        vm.HasChanges.ShouldBeFalse();

        vm.HasChanges = true;

        vm.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public void Summary_IsTheUncommittedChangesLabel()
    {
        var vm = new WorkingTreeRowViewModel();

        vm.Summary.ShouldBe("Uncommitted changes");
    }

    [Fact]
    public void IsSelected_RaisesPropertyChanged()
    {
        var vm = new WorkingTreeRowViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkingTreeRowViewModel.IsSelected))
                raised = true;
        };

        vm.IsSelected = true;

        raised.ShouldBeTrue();
    }
}
