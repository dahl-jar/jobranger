using Jobranger.Cli.Http;

namespace Jobranger.Cli.Tests.Http;

public sealed class RetryAfterTests {
    private static readonly DateTimeOffset now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("120", 120)]
    [InlineData(" 45 ", 45)]
    [InlineData("0", 0)]
    [InlineData("301", 300)]
    [InlineData("99999999999999999999", 300)]
    [InlineData("Mon, 12 Oct 2026 09:00:30 GMT", 30)]
    [InlineData("Mon, 12 Oct 2026 08:59:00 GMT", 0)]
    [InlineData("Mon, 12 Oct 2026 10:00:00 GMT", 300)]
    public void ShouldReadDelay_WhenHeaderIsValid(string header, int seconds) {
        Assert.Equal(TimeSpan.FromSeconds(seconds), RetryAfter.Parse(header, now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("soon")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("١٢")]
    public void ShouldReturnNull_WhenHeaderIsUnreadable(string? header) {
        Assert.Null(RetryAfter.Parse(header, now));
    }
}
