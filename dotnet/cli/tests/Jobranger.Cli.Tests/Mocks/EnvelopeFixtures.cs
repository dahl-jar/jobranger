using Jobranger.Messaging.Envelopes;

namespace Jobranger.Cli.Tests.Mocks;

internal static class EnvelopeFixtures {
    public const string Sid = "AAAAAAAAAAAAAAAAAAAAAA";
    public const string EmptySealed = "AAAAAAAAAAAAAAAAAAAAAA";

    private const int version = 1;
    private const string helloKind = "hello";
    private const string dataKind = "data";

    public static readonly string CommitStep = FakeBrowserPeer.Encode("{\"t\":\"commit\"}");

    public static Envelope Hello(string channel, string sid, string? step = null, long seq = 0) =>
        new(version, channel, sid, seq, helloKind, step ?? CommitStep);

    public static Envelope Data(string channel, string sid, long seq, string ciphertext) =>
        new(version, channel, sid, seq, dataKind, ciphertext);
}
