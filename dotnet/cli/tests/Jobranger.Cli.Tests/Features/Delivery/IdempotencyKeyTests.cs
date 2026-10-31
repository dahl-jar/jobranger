using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Delivery;

public sealed class IdempotencyKeyTests {
    private const long runId = 7;
    private const string changed = "changed";

    private static readonly RenderedDigest baseDigest = new("Subject", "<p>html</p>", "text", []);

    [Fact]
    public void ShouldEqualKnownHash_WhenPartsAreGiven() {
        RenderedDigest rendered = new("Subject Ø", "<p>html</p>", "text\n", []);
        EmailAddresses addresses = new("jobranger <sender@example.test>", "user@example.test");

        string key = IdempotencyKey.For(runId, rendered, addresses);

        Assert.Equal("29faeee0759fc620f6c574308cb065c0f8bd37e30190d5479a57660ffe2086b6", key);
    }

    [Theory]
    [InlineData("ab", "c", "1cbeab467b42442a988e21065f1a812f6bbf75ddc0c78ccf8a73d8bd6207ce5b")]
    [InlineData("a", "bc", "dacb8eeef484bc3c43a696421c0c6bccc4347094227fb2fad810a727879d3dfe")]
    public void ShouldChangeKey_WhenPartBoundaryMoves(string subject, string text, string expected) {
        RenderedDigest rendered = new(subject, "h", text, []);

        string key = IdempotencyKey.For(runId, rendered, new EmailAddresses("s", "t"));

        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData("run")]
    [InlineData("subject")]
    [InlineData("text")]
    [InlineData("html")]
    [InlineData("sender")]
    [InlineData("to")]
    public void ShouldChangeKey_WhenOnePartChanges(string part) {
        string original = IdempotencyKey.For(runId, baseDigest, DeliveryFixtures.Addresses);

        string key = IdempotencyKey.For(
            part == "run" ? runId + 1 : runId,
            baseDigest with {
                Subject = part == "subject" ? changed : baseDigest.Subject,
                Text = part == "text" ? changed : baseDigest.Text,
                Html = part == "html" ? changed : baseDigest.Html,
            },
            DeliveryFixtures.Addresses with {
                Sender = part == "sender" ? changed : DeliveryFixtures.Addresses.Sender,
                To = part == "to" ? changed : DeliveryFixtures.Addresses.To,
            });

        Assert.NotEqual(original, key);
    }

    [Fact]
    public void ShouldIgnoreAdIds_WhenKeyIsBuilt() {
        string original = IdempotencyKey.For(runId, baseDigest, DeliveryFixtures.Addresses);

        string key = IdempotencyKey.For(runId, baseDigest with { AdIds = ["finn:1"] }, DeliveryFixtures.Addresses);

        Assert.Equal(original, key);
    }
}
