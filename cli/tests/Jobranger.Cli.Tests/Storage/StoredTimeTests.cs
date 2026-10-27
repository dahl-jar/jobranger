using Jobranger.Cli.Storage;

namespace Jobranger.Cli.Tests.Storage;

public sealed class StoredTimeTests {
    [Theory]
    [InlineData("2026-10-09T10:00:00.2500000+00:00", "2026-10-09T10:00:00.250000+00:00")]
    [InlineData("2026-10-09T10:00:00+00:00", "2026-10-09T10:00:00+00:00")]
    [InlineData("2026-10-09T12:00:00+02:00", "2026-10-09T10:00:00+00:00")]
    public void ShouldWritePythonIsoTime_WhenTimeIsFormatted(string time, string expected) {
        Assert.Equal(expected, StoredTime.Format(DateTimeOffset.Parse(time, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void ShouldWriteIsoDate_WhenDayIsFormatted() {
        Assert.Equal("2026-01-05", StoredTime.Format(new DateOnly(2026, 1, 5)));
    }
}
