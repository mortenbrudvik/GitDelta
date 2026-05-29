using GitDelta.Core.Models;
using GitDelta.UI.ViewModels;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.ViewModels;

public class CommitRowViewModelTests
{
    private static CommitInfo MakeCommit(
        string sha = "1234567890abcdef1234567890abcdef12345678",
        string subject = "Initial commit",
        string authorName = "Ada Lovelace") =>
        new(
            Sha: sha,
            ShortSha: sha[..7],
            Parents: [],
            AuthorName: authorName,
            AuthorEmail: "ada@example.com",
            AuthorDate: new DateTimeOffset(2026, 5, 29, 10, 0, 0, TimeSpan.Zero),
            CommitterName: authorName,
            CommitterEmail: "ada@example.com",
            CommitDate: new DateTimeOffset(2026, 5, 29, 10, 0, 0, TimeSpan.Zero),
            Subject: subject,
            Body: "Body text");

    [Fact]
    public void Constructor_CopiesShaSubjectAndAuthorFromCommit()
    {
        var vm = new CommitRowViewModel(MakeCommit());

        vm.Sha.ShouldBe("1234567890abcdef1234567890abcdef12345678");
        vm.ShortSha.ShouldBe("1234567");
        vm.Subject.ShouldBe("Initial commit");
        vm.Author.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public void FullDate_FormatsAuthorDateAsLocalReadableString()
    {
        var vm = new CommitRowViewModel(MakeCommit());

        // FullDate must be non-empty and contain the year.
        vm.FullDate.ShouldNotBeNullOrWhiteSpace();
        vm.FullDate.ShouldContain("2026");
    }

    [Fact]
    public void RelativeDate_IsNonEmpty()
    {
        var vm = new CommitRowViewModel(MakeCommit());

        vm.RelativeDate.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void IsSelected_DefaultsToFalse_AndIsSettable()
    {
        var vm = new CommitRowViewModel(MakeCommit());

        vm.IsSelected.ShouldBeFalse();

        vm.IsSelected = true;

        vm.IsSelected.ShouldBeTrue();
    }

    [Fact]
    public void IsSelected_RaisesPropertyChanged()
    {
        var vm = new CommitRowViewModel(MakeCommit());
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommitRowViewModel.IsSelected))
                raised = true;
        };

        vm.IsSelected = true;

        raised.ShouldBeTrue();
    }
}
