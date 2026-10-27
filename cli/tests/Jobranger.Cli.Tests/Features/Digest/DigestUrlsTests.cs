using Jobranger.Cli.Features.Digest;

namespace Jobranger.Cli.Tests.Features.Digest;

public sealed class DigestUrlsTests {
    private const string adBase = "https://ads.example.test/";
    private const int pathAtLimit = 2023;
    private const int quotedLetters = 400;
    private const int urlLimit = 2048;

    public static TheoryData<string> BrokenUrls() => [
        "ftp://ads.example.test/x",
        "https://",
        "https://user@ads.example.test/x",
        "https://ads.exa\u200Bmple.test/x",
        "https://[evil/x",
        "https://[1.2.3.4]/x",
        "https://ads\uFF0Fexample.test/x",
        adBase + "x" + new string(' ', urlLimit),
        adBase + new string('\u00E9', quotedLetters),
    ];

    [Theory]
    [MemberData(nameof(BrokenUrls))]
    public void ShouldDropAdLink_WhenUrlBreaksWebRule(string url) {
        Assert.Null(DigestUrls.Web(url));
    }

    [Theory]
    [InlineData("  https://ads.example.test/x  ", "https://ads.example.test/x")]
    [InlineData("\u0001https://ads.example.test/x", "https://ads.example.test/x")]
    [InlineData("HTTPS://ads.example.test/x", "https://ads.example.test/x")]
    [InlineData("https://ads.exam\tple.test/x", "https://ads.example.test/x")]
    [InlineData("https://[::1]/x", "https://[::1]/x")]
    [InlineData("https://[::1%eth0]/x", "https://[::1%eth0]/x")]
    [InlineData("https://[v1.x]/x", "https://[v1.x]/x")]
    public void ShouldWriteCleanUrl_WhenUrlIsAccepted(string url, string expected) {
        Assert.Equal(expected, DigestUrls.Web(url)!.Text);
    }

    [Fact]
    public void ShouldReplaceLoneSurrogate_WhenUrlHasOne() {
        string url = adBase + "p" + '\uD800';

        Assert.Equal(adBase + "p", DigestUrls.Web(url)!.Text);
    }

    [Fact]
    public void ShouldKeepAdLink_WhenUrlIsAtLengthLimit() {
        string url = adBase + new string('p', pathAtLimit);

        Assert.Equal(url, DigestUrls.Web(url)!.Text);
    }
}
