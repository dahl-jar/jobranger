using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Secrets;

public sealed class KeyGuardTests {
    private const string key = "sk-fake-0000-test-key-1234";
    private const int fullwidthOffset = 0xFEE0;
    private const int mathBoldSmallA = 0x1D41A;
    private const int mathBoldZero = 0x1D7CE;

    [Theory]
    [InlineData("bad " + key)]
    [InlineData("bad SK-FAKE-0000-TEST-KEY-1234")]
    [InlineData("bad sk%2Dfake%2D0000%2Dtest%2Dkey%2D1234")]
    [InlineData("bad sk%252Dfake%252D0000%252Dtest%252Dkey%252D1234")]
    [InlineData("bad sk&#45;fake&#45;0000&#45;test&#45;key&#45;1234")]
    [InlineData("bad sk-fake-00 00-test-k ey-1234")]
    [InlineData("bad test-key-1234")]
    [InlineData("bad fake-0000-test")]
    [InlineData("bad &#115&#107&#45&#102&#97&#107&#101&#45&#48&#48&#48&#48&#45&#116&#101&#115&#116&#45&#107&#101&#121&#45&#49&#50&#51&#52")]
    [InlineData("bad sk-fak&#x65-0000&#x2dtest&#x2dkey-1234")]
    public void ShouldDropDetail_WhenItHoldsKeyFragment(string detail) {
        string? cleaned = KeyGuard.Clean(detail, key);

        Assert.Null(cleaned);
    }

    [Fact]
    public void ShouldDropDetail_WhenKeyIsWrittenInFullwidth() {
        string fullwidth = string.Concat(key.Select(character => (char)(character + fullwidthOffset)));

        string? cleaned = KeyGuard.Clean("bad " + fullwidth, key);

        Assert.Null(cleaned);
    }

    [Fact]
    public void ShouldDropDetail_WhenKeyIsWrittenInMathBold() {
        string mathBold = string.Concat(key.Select(MathBold));

        string? cleaned = KeyGuard.Clean("bad " + mathBold, key);

        Assert.Null(cleaned);
    }

    [Fact]
    public void ShouldKeepEmoji_WhenCleaningDetail() {
        string? cleaned = KeyGuard.Clean("Rate limit \U0001F600 reached", key);

        Assert.Equal("Rate limit \U0001F600 reached", cleaned);
    }

    [Fact]
    public void ShouldDropDetail_WhenKeySitsPastTheCut() {
        string detail = new string('a', KeyGuard.DetailMaxChars - 3) + key;

        string? cleaned = KeyGuard.Clean(detail, key);

        Assert.Null(cleaned);
    }

    [Theory]
    [InlineData("bad &#xd800 request")]
    [InlineData("bad &#99999999999 request")]
    public void ShouldKeepDetail_WhenCharRefIsOutOfRange(string detail) {
        string? cleaned = KeyGuard.Clean(detail, key);

        Assert.Equal(detail, cleaned);
    }

    [Theory]
    [InlineData("bad sk-fake&#128-0000-test&#128-key-1234")]
    [InlineData("bad sk-fake&#0-0000-test&#0-key-1234")]
    public void ShouldKeepDetail_WhenReplacedCharRefSplitsKey(string detail) {
        string? cleaned = KeyGuard.Clean(detail, key);

        Assert.Equal(detail, cleaned);
    }

    [Fact]
    public void ShouldKeepDetail_WhenItHoldsElevenKeyChars() {
        string? cleaned = KeyGuard.Clean("unknown " + SecretFixtures.EdgeAsciiKey[1..], SecretFixtures.EdgeAsciiKey);

        Assert.Equal("unknown fake-edges~", cleaned);
    }

    [Fact]
    public void ShouldStripControlCharacters_WhenCleaningDetail() {
        string? cleaned = KeyGuard.Clean("bad\u001b[31m request\u0007 here\u202E", key);

        Assert.Equal("bad[31m request here", cleaned);
    }

    [Fact]
    public void ShouldJoinWhiteSpaceRuns_WhenCleaningDetail() {
        string? cleaned = KeyGuard.Clean("  bad \n\t request  ", key);

        Assert.Equal("bad request", cleaned);
    }

    [Fact]
    public void ShouldReturnNull_WhenDetailIsBlank() {
        string? cleaned = KeyGuard.Clean(" \n ", key);

        Assert.Null(cleaned);
    }

    [Fact]
    public void ShouldCutDetail_WhenLongerThanLimit() {
        string? cleaned = KeyGuard.Clean(new string('a', KeyGuard.DetailMaxChars + 1), key);

        Assert.Equal(new string('a', KeyGuard.DetailMaxChars), cleaned);
    }

    private static string MathBold(char character) => character switch {
        >= 'a' and <= 'z' => char.ConvertFromUtf32(mathBoldSmallA + character - 'a'),
        >= '0' and <= '9' => char.ConvertFromUtf32(mathBoldZero + character - '0'),
        _ => new string(character, 1),
    };
}
