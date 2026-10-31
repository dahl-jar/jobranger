using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Text;

public sealed class CanonicalUrlsTests {
    [Theory]
    [InlineData("https://\u0130.example.test/1", "https://i\u0307.example.test/1")]
    [InlineData("https://\u0386\u03A3/1", "https://\u03AC\u03C2/1")]
    [InlineData("https://\u0386\u03A3.example.test/1", "https://\u03AC\u03C3.example.test/1")]
    public void ShouldLowerHostLikePython_WhenHostHasSpecialCasing(string url, string expected) {
        Assert.Equal(expected, CanonicalUrls.Of(url));
    }

    [Fact]
    public void ShouldKeepHost_WhenPathIsEmpty() {
        string canonical = CanonicalUrls.Of("https://Ads.example.test");

        Assert.Equal("https://ads.example.test", canonical);
    }

    [Fact]
    public void ShouldKeepHostCase_WhenSchemeIsInvalid() {
        string canonical = CanonicalUrls.Of("h!tp://Host/x");

        Assert.EndsWith("h!tp://Host/x", canonical, StringComparison.Ordinal);
    }
}
