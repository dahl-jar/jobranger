using System.Buffers.Text;
using System.Text;
using Jobranger.Cli.Features.Sessions;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Sessions;

public sealed class HelloStepsTests {
    private const string key = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string shortKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string looseKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB";
    private const string nonce = "AAAAAAAAAAAAAAAAAAAAAA";
    private const string pairTag = "12UQIiI7bA2K2czifheB8w";
    private const int helloMaxBytes = 4096;
    private const int agentMaxBytes = 256;

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"t\":\"offer\",\"cli_static\":\"" + key + "\",\"cli_eph\":\"" + key + "\"}")]
    [InlineData("{\"t\":\"reveal\",\"browser_eph\":\"" + key + "\",\"agent\":\"a\"}")]
    [InlineData("{\"t\":\"abort\",\"x\":\"1\"}")]
    [InlineData("{\"t\":\"reveal\",\"browser_eph\":\"" + key + "\",\"nonce_b\":\"" + nonce + "\",\"agent\":null}")]
    [InlineData("{\"t\":\"abort\",\"t\":\"abort\"}")]
    [InlineData("{\"t\":\"confirm\",\"mac\":\"" + shortKey + "\"}")]
    [InlineData("{\"t\":\"confirm\",\"mac\":\"" + looseKey + "\"}")]
    [InlineData("{\"t\":\"reveal\",\"browser_eph\":\"" + key + "\",\"nonce_b\":\"" + nonce + "\",\"agent\":\"\\ud800\"}")]
    [InlineData("{\"t\":\"abort\"}{}")]
    [InlineData("{\"commit\":\"x\"}")]
    public void ShouldRejectStep_WhenJsonBreaksStepRules(string json) {
        HandshakeException error = Assert.Throws<HandshakeException>(() => HelloSteps.Parse(FakeBrowserPeer.Encode(json)));

        Assert.Equal(HandshakeFailure.Step, error.Reason);
    }

    [Fact]
    public void ShouldRejectStep_WhenOver4096Bytes() {
        string json = "{\"t\":\"abort\"}".PadRight(helloMaxBytes + 1);

        HandshakeException error = Assert.Throws<HandshakeException>(() => HelloSteps.Parse(FakeBrowserPeer.Encode(json)));

        Assert.Equal(HandshakeFailure.Step, error.Reason);
    }

    [Fact]
    public void ShouldAcceptStep_WhenExactly4096Bytes() {
        string json = "{\"t\":\"abort\"}".PadRight(helloMaxBytes);

        HelloSteps.Step step = HelloSteps.Parse(FakeBrowserPeer.Encode(json));

        Assert.IsType<HelloSteps.Abort>(step);
    }

    [Fact]
    public void ShouldRejectStep_WhenTextIsNotUtf8() {
        byte[] utf8 = Utf8Fixtures.WithInvalidByte("{\"t\":\"abort", "\"}");

        HandshakeException error = Assert.Throws<HandshakeException>(() => HelloSteps.Parse(FakeBrowserPeer.Encode(utf8)));

        Assert.Equal(HandshakeFailure.Step, error.Reason);
    }

    [Fact]
    public void ShouldRejectStep_WhenCiphertextIsNotCanonical() {
        HandshakeException error = Assert.Throws<HandshakeException>(() => HelloSteps.Parse("eyJ0IjoiYWJvcnQifQ=="));

        Assert.Equal(HandshakeFailure.Step, error.Reason);
    }

    [Fact]
    public void ShouldRejectReveal_WhenAgentIsOver256Bytes() {
        string json = Reveal(new string('a', agentMaxBytes + 1));

        HandshakeException error = Assert.Throws<HandshakeException>(() => HelloSteps.Parse(FakeBrowserPeer.Encode(json)));

        Assert.Equal(HandshakeFailure.Step, error.Reason);
    }

    [Fact]
    public void ShouldAcceptReveal_WhenAgentIs256Bytes() {
        string agent = new('a', agentMaxBytes);

        HelloSteps.Step step = HelloSteps.Parse(FakeBrowserPeer.Encode(Reveal(agent)));

        Assert.Equal(agent, Assert.IsType<HelloSteps.Reveal>(step).Agent);
    }

    [Fact]
    public void ShouldKeepPairTag_WhenCommitCarriesOne() {
        string json = "{\"t\":\"commit\",\"browser_static\":\"" + key + "\",\"commit\":\"" + key
            + "\",\"pair_tag\":\"" + pairTag + "\"}";

        HelloSteps.Step step = HelloSteps.Parse(FakeBrowserPeer.Encode(json));

        Assert.Equal(Base64Url.DecodeFromChars(pairTag), Assert.IsType<HelloSteps.Commit>(step).PairTag);
    }

    [Fact]
    public void ShouldWriteBareStep_WhenEncodingClosingSteps() {
        string[] written = [
            Json(HelloSteps.Encode(new HelloSteps.Abort())),
            Json(HelloSteps.Encode(new HelloSteps.UnknownDevice())),
            Json(HelloSteps.Encode(new HelloSteps.UnknownSession())),
        ];

        Assert.Equal(["{\"t\":\"abort\"}", "{\"t\":\"unknown_device\"}", "{\"t\":\"unknown_session\"}"], written);
    }

    private static string Reveal(string agent) =>
        "{\"t\":\"reveal\",\"browser_eph\":\"" + key + "\",\"nonce_b\":\"" + nonce + "\",\"agent\":\"" + agent + "\"}";

    private static string Json(string step) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(step));
}
