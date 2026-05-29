using System.Text;
using GitDelta.Core.Git.Parsing;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Git.Parsing;

public class GitLogParserTests
{
    private const char US = '\u001f'; // 0x1f field separator (%x1f)
    private const char NUL = '\0'; // 0x00 record terminator (-z)

    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    /// <summary>
    /// Builds one log record: 10 %x1f-separated fields. Caller appends NUL between records.
    /// </summary>
    private static string Record(
        string sha, string parents, string an, string ae, string aDate,
        string cn, string ce, string cDate, string subject, string body)
        => string.Join(US, sha, parents, an, ae, aDate, cn, ce, cDate, subject, body);

    [Fact]
    public void Parse_NormalCommit_MapsAllFields()
    {
        var record = Record(
            "1111111111111111111111111111111111111111",
            "2222222222222222222222222222222222222222",
            "Alice", "alice@example.com", "2026-05-29T10:00:00+02:00",
            "Bob", "bob@example.com", "2026-05-29T11:30:00+02:00",
            "Add feature", "Detailed body line.");
        var stdout = Bytes(record + NUL);

        var commits = GitLogParser.Parse(stdout);

        commits.Count.ShouldBe(1);
        var c = commits[0];
        c.Sha.ShouldBe("1111111111111111111111111111111111111111");
        c.ShortSha.ShouldBe("1111111");
        c.Parents.ShouldBe(["2222222222222222222222222222222222222222"]);
        c.AuthorName.ShouldBe("Alice");
        c.AuthorEmail.ShouldBe("alice@example.com");
        c.AuthorDate.ShouldBe(DateTimeOffset.Parse("2026-05-29T10:00:00+02:00"));
        c.CommitterName.ShouldBe("Bob");
        c.CommitterEmail.ShouldBe("bob@example.com");
        c.CommitDate.ShouldBe(DateTimeOffset.Parse("2026-05-29T11:30:00+02:00"));
        c.Subject.ShouldBe("Add feature");
        c.Body.ShouldBe("Detailed body line.");
        c.IsMerge.ShouldBeFalse();
        c.IsRoot.ShouldBeFalse();
    }

    [Fact]
    public void Parse_MultipleRecords_ReturnsInOrder()
    {
        var r1 = Record("aaaaaaa1", "", "A", "a@x", "2026-01-01T00:00:00Z",
            "A", "a@x", "2026-01-01T00:00:00Z", "first", "");
        var r2 = Record("bbbbbbb2", "aaaaaaa1", "B", "b@x", "2026-01-02T00:00:00Z",
            "B", "b@x", "2026-01-02T00:00:00Z", "second", "");
        var stdout = Bytes(r1 + NUL + r2 + NUL);

        var commits = GitLogParser.Parse(stdout);

        commits.Count.ShouldBe(2);
        commits[0].Subject.ShouldBe("first");
        commits[1].Subject.ShouldBe("second");
    }

    [Fact]
    public void Parse_MergeCommit_HasMultipleParents()
    {
        var record = Record("merge123", "p1aaaaa p2bbbbb", "M", "m@x", "2026-05-01T00:00:00Z",
            "M", "m@x", "2026-05-01T00:00:00Z", "Merge branch", "");
        var stdout = Bytes(record + NUL);

        var commits = GitLogParser.Parse(stdout);

        commits[0].Parents.ShouldBe(["p1aaaaa", "p2bbbbb"]);
        commits[0].IsMerge.ShouldBeTrue();
    }

    [Fact]
    public void Parse_OctopusMerge_HasThreeParents()
    {
        var record = Record("octo123", "p1 p2 p3", "M", "m@x", "2026-05-01T00:00:00Z",
            "M", "m@x", "2026-05-01T00:00:00Z", "Octopus", "");
        var stdout = Bytes(record + NUL);

        var commits = GitLogParser.Parse(stdout);

        commits[0].Parents.ShouldBe(["p1", "p2", "p3"]);
        commits[0].IsMerge.ShouldBeTrue();
    }

    [Fact]
    public void Parse_RootCommit_HasNoParents()
    {
        var record = Record("root999", "", "R", "r@x", "2026-01-01T00:00:00Z",
            "R", "r@x", "2026-01-01T00:00:00Z", "Initial commit", "");
        var stdout = Bytes(record + NUL);

        var commits = GitLogParser.Parse(stdout);

        commits[0].Parents.ShouldBeEmpty();
        commits[0].IsRoot.ShouldBeTrue();
    }

    [Fact]
    public void Parse_MultiLineBodyWithUnicode_PreservedDecodedAsUtf8()
    {
        var record = Record("u8sha12", "parent1", "Renée", "renee@example.com", "2026-05-29T10:00:00Z",
            "Renée", "renee@example.com", "2026-05-29T10:00:00Z",
            "Fix café crash", "Line one\nLine two — em dash\nLine three");
        var stdout = Bytes(record + NUL);

        var commits = GitLogParser.Parse(stdout);

        commits[0].AuthorName.ShouldBe("Renée");
        commits[0].Subject.ShouldBe("Fix café crash");
        commits[0].Body.ShouldBe("Line one\nLine two — em dash\nLine three");
    }

    [Fact]
    public void Parse_BodyContainingFieldSeparator_IsNotTruncated()
    {
        // A commit body that itself contains a 0x1f must not split the record into
        // extra fields; capping the split at FieldCount keeps the separator in the body.
        var bodyWithSeparator = "before" + US + "after";
        var record = Record("body1f0", "parent1", "A", "a@x", "2026-01-01T00:00:00Z",
            "A", "a@x", "2026-01-01T00:00:00Z", "subject", bodyWithSeparator);
        var stdout = Bytes(record + NUL);

        var commits = GitLogParser.Parse(stdout);

        commits.Count.ShouldBe(1);
        commits[0].Subject.ShouldBe("subject");
        commits[0].Body.ShouldBe(bodyWithSeparator);
    }

    [Fact]
    public void Parse_EmptyOutput_ReturnsEmptyList()
    {
        GitLogParser.Parse([]).ShouldBeEmpty();
    }

    [Fact]
    public void Parse_RecordWithUnparseableDate_IsSkippedWithoutThrowing()
    {
        // A malformed date (unexpected git config/locale) must not crash history loading;
        // skip the offending record and keep the valid ones.
        var good = Record("good111", "", "A", "a@x", "2026-01-01T00:00:00Z",
            "A", "a@x", "2026-01-01T00:00:00Z", "good", "");
        var bad = Record("bad2222", "", "B", "b@x", "not-a-date",
            "B", "b@x", "not-a-date", "bad", "");
        var stdout = Bytes(good + NUL + bad + NUL);

        var commits = GitLogParser.Parse(stdout);

        commits.Count.ShouldBe(1);
        commits[0].Sha.ShouldBe("good111");
    }

    [Fact]
    public void Parse_TrailingTerminatorOnly_ProducesNoPhantomRecord()
    {
        var record = Record("only111", "", "A", "a@x", "2026-01-01T00:00:00Z",
            "A", "a@x", "2026-01-01T00:00:00Z", "only", "");
        var stdout = Bytes(record + NUL);

        GitLogParser.Parse(stdout).Count.ShouldBe(1);
    }
}
