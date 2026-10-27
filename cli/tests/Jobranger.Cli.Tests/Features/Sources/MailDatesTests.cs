using System.Globalization;
using Jobranger.Cli.Features.Sources;

namespace Jobranger.Cli.Tests.Features.Sources;

public sealed class MailDatesTests {
    [Theory]
    [InlineData("Mon, 24 Aug 2026 08:15:00 +0200", "2026-08-24T06:15:00+00:00")]
    [InlineData("Mon, 24 Aug 2026 04:15:00 -0400", "2026-08-24T08:15:00+00:00")]
    [InlineData("24 Aug 2026 08:15 +0000", "2026-08-24T08:15:00+00:00")]
    [InlineData("Mon, 24 Aug 2026 08:15:42 +0530", "2026-08-24T02:45:42+00:00")]
    public void ShouldParseDate_WhenZoneIsNumeric(string text, string expected) {
        DateTimeOffset? parsed = MailDates.Parse(text);

        Assert.Equal(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture), parsed);
    }

    [Theory]
    [InlineData("Mon, 24 Aug 2026 04:15:00 EDT", "2026-08-24T08:15:00+00:00")]
    [InlineData("Mon, 24 Aug 2026 00:15:00 pst", "2026-08-24T08:15:00+00:00")]
    public void ShouldParseDate_WhenZoneIsNamed(string text, string expected) {
        DateTimeOffset? parsed = MailDates.Parse(text);

        Assert.Equal(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture), parsed);
    }

    [Fact]
    public void ShouldReadAsUtc_WhenZoneIsUnknown() {
        DateTimeOffset? parsed = MailDates.Parse("Mon, 24 Aug 2026 08:15:00 CEST");

        Assert.Equal(new DateTimeOffset(2026, 8, 24, 8, 15, 0, TimeSpan.Zero), parsed);
    }

    [Theory]
    [InlineData("Mon, 24 Aug 68 08:15:00 +0000", 2068)]
    [InlineData("Mon, 24 Aug 69 08:15:00 +0000", 1969)]
    public void ShouldExpandYear_WhenYearHasTwoDigits(string text, int year) {
        DateTimeOffset? parsed = MailDates.Parse(text);

        Assert.Equal(year, parsed?.Year);
    }

    [Theory]
    [InlineData("snarest")]
    [InlineData("Mon, 31 Feb 2026 08:15:00 +0000")]
    [InlineData("Mon, 24 Foo 2026 08:15:00 +0000")]
    public void ShouldReturnNull_WhenDateIsUnreadable(string text) {
        DateTimeOffset? parsed = MailDates.Parse(text);

        Assert.Null(parsed);
    }
}
