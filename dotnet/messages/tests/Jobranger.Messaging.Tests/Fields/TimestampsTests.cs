using Jobranger.Messaging.Fields;
using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Tests.Fields;

public sealed class TimestampsTests {
    [Fact]
    public void ShouldKeepOffset_WhenTimestampHasOne() {
        DateTimeOffset parsed = Timestamps.Parse("2026-10-09T03:00:00.5+02:00");

        Assert.Equal(new DateTimeOffset(2026, 10, 9, 3, 0, 0, 500, TimeSpan.FromHours(2)), parsed);
        Assert.Equal(TimeSpan.FromHours(2), parsed.Offset);
    }

    [Fact]
    public void ShouldParseTimestamp_WhenFractionHasNineDigits() {
        DateTimeOffset parsed = Timestamps.Parse("2026-10-09T03:00:00.123456789Z");

        Assert.Equal(new DateTimeOffset(2026, 10, 9, 3, 0, 0, TimeSpan.Zero).AddTicks(1234567), parsed);
    }

    [Theory]
    [InlineData("2026-02-30T00:00:00Z")]
    [InlineData("0000-01-01T00:00:00Z")]
    [InlineData("2026-10-09 03:00:00Z")]
    [InlineData("2026-10-09T03:00:00")]
    [InlineData("2026-10-09T03:00:00Z\n")]
    public void ShouldRejectTimestamp_WhenTextIsNotRfc3339(string text) {
        Assert.Throws<ContractException>(() => Timestamps.Parse(text));
    }
}
