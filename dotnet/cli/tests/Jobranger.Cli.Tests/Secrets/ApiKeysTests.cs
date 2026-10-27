using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Secrets;

public sealed class ApiKeysTests {
    private const string label = "OpenAI";

    [Fact]
    public void ShouldReturnStrippedKey_WhenKeyIsPrintableAscii() {
        string key = ApiKeys.Require(label, " " + SecretFixtures.EdgeAsciiKey + "\n");

        Assert.Equal(SecretFixtures.EdgeAsciiKey, key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" \t")]
    public void ShouldRefuseKey_WhenNoneIsSaved(string? saved) {
        ArgumentException refused = Assert.Throws<ArgumentException>(() => ApiKeys.Require(label, saved));

        Assert.Equal("OpenAI needs an API key", refused.Message);
    }

    [Theory]
    [InlineData("fake key-12345")]
    [InlineData("fake\u007fkey-12345")]
    public void ShouldRefuseKey_WhenCharIsOutsidePrintableAscii(string saved) {
        ArgumentException refused = Assert.Throws<ArgumentException>(() => ApiKeys.Require(label, saved));

        Assert.Equal("OpenAI API key must be printable ASCII", refused.Message);
    }

    [Fact]
    public void ShouldRefuseKey_WhenShorterThanTwelveChars() {
        ArgumentException refused =
            Assert.Throws<ArgumentException>(() => ApiKeys.Require(label, SecretFixtures.EdgeAsciiKey[1..]));

        Assert.Equal("OpenAI API key is too short", refused.Message);
    }
}
