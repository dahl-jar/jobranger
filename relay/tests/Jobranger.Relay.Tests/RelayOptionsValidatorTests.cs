using Jobranger.Relay.Options;

namespace Jobranger.Relay.Tests;

public sealed class RelayOptionsValidatorTests {
    private const string shortKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";

    private readonly RelayOptionsValidator validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ShouldAcceptValidOptions(string? previousHandleKey) {
        RelayOptions options = Valid();
        options.PreviousHandleKey = previousHandleKey;

        bool failed = validator.Validate(null, options).Failed;

        Assert.False(failed);
    }

    [Theory]
    [InlineData(shortKey)]
    [InlineData("not base64 at all, but long enough to pass a length check!!")]
    public void ShouldRejectShortOrMalformedHandleKey(string key) {
        RelayOptions options = Valid();
        options.HandleKey = key;

        bool failed = validator.Validate(null, options).Failed;

        Assert.True(failed);
    }

    [Fact]
    public void ShouldRejectShortPreviousHandleKey() {
        RelayOptions options = Valid();
        options.PreviousHandleKey = shortKey;

        bool failed = validator.Validate(null, options).Failed;

        Assert.True(failed);
    }

    [Fact]
    public void ShouldRejectShortRedactionKey() {
        RelayOptions options = Valid();
        options.RedactionKey = shortKey;

        bool failed = validator.Validate(null, options).Failed;

        Assert.True(failed);
    }

    [Theory]
    [InlineData("site.test")]
    [InlineData("https://site.test/")]
    [InlineData("ftp://site.test")]
    public void ShouldRejectSiteOriginThatIsNotAnOrigin(string origin) {
        RelayOptions options = Valid();
        options.SiteOrigin = origin;

        bool failed = validator.Validate(null, options).Failed;

        Assert.True(failed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("10.42.0.0/16,not-a-network")]
    public void ShouldRejectMissingOrInvalidProxyNetworks(string networks) {
        RelayOptions options = Valid();
        options.KnownProxyNetworks = networks.Split(',', StringSplitOptions.RemoveEmptyEntries);

        bool failed = validator.Validate(null, options).Failed;

        Assert.True(failed);
    }

    private static RelayOptions Valid() => new() {
        SiteOrigin = "http://localhost:5173",
        HandleKey = RelayFactory.HandleKey,
        RedactionKey = RelayFactory.RedactionKey,
        KnownProxyNetworks = ["10.42.0.0/16"],
    };
}
