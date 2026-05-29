using GitDelta.Core.Models;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Models;

public sealed class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 29, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(5, "just now")]            // 5 seconds ago
    [InlineData(45, "just now")]           // < 1 minute
    [InlineData(60, "1 minute ago")]
    [InlineData(150, "2 minutes ago")]
    [InlineData(3600, "1 hour ago")]
    [InlineData(7200, "2 hours ago")]
    [InlineData(86400, "yesterday")]
    [InlineData(172800, "2 days ago")]
    [InlineData(604800, "last week")]
    [InlineData(1209600, "2 weeks ago")]
    [InlineData(2592000, "last month")]    // 30 days — singular month boundary
    [InlineData(31536000, "last year")]    // 365 days — singular year boundary
    public void Format_ReturnsExpectedPhrase(int secondsAgo, string expected)
    {
        var when = Now.AddSeconds(-secondsAgo);

        RelativeTime.Format(when, Now).ShouldBe(expected);
    }

    [Fact]
    public void Format_MonthsAgo_ReturnsMonths()
    {
        var when = Now.AddDays(-70);

        RelativeTime.Format(when, Now).ShouldBe("2 months ago");
    }

    [Fact]
    public void Format_YearsAgo_ReturnsYears()
    {
        var when = Now.AddDays(-800);

        RelativeTime.Format(when, Now).ShouldBe("2 years ago");
    }

    [Fact]
    public void Format_FutureTimestamp_ClampsToJustNow()
    {
        var when = Now.AddSeconds(30);

        RelativeTime.Format(when, Now).ShouldBe("just now");
    }
}
