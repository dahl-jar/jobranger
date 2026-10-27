using Jobranger.Cli.Config;

namespace Jobranger.Cli.Tests.Config;

public sealed class ServiceUrlsTests {
    [Fact]
    public void ShouldUseProductionUrls_WhenNoOverrideIsSet() {
        ServiceUrls urls = ServiceUrls.From(new Dictionary<string, string>());

        Assert.Equal(
            ("https://jobranger.no/", "https://relay.jobranger.no/"), (urls.Site.ToString(), urls.Relay.ToString()));
    }

    [Fact]
    public void ShouldUseOverrides_WhenVariablesAreSet() {
        ServiceUrls urls = ServiceUrls.From(new Dictionary<string, string> {
            ["JOBRANGER_SITE_URL"] = "http://localhost:5173/",
            ["JOBRANGER_RELAY_URL"] = "http://127.0.0.1:8080",
        });

        Assert.Equal(
            ("http://localhost:5173/", "http://127.0.0.1:8080/"), (urls.Site.ToString(), urls.Relay.ToString()));
    }

    [Theory]
    [InlineData("ftp://relay.example.test")]
    [InlineData("relay.example.test")]
    [InlineData("http://relay.example.test")]
    [InlineData("https://user:pass-1234@relay.example.test")]
    [InlineData("https://relay.example.test/?token=pass-1234")]
    [InlineData("https://relay.example.test/#pass-1234")]
    public void ShouldRejectOverride_WhenItIsNotPlainHttpUrl(string url) {
        ConfigInvalidException invalid = Assert.Throws<ConfigInvalidException>(
            () => ServiceUrls.From(new Dictionary<string, string> { ["JOBRANGER_RELAY_URL"] = url }));

        Assert.Equal([new ConfigError("JOBRANGER_RELAY_URL", ConfigProblem.NotServiceUrl)], invalid.Errors);
    }
}
