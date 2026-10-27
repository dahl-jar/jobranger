using System.Buffers.Text;
using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Relay.Core;
using NSec.Cryptography;

namespace Jobranger.Relay.Tests;

internal sealed class CliKey : IDisposable {
    public const string PairStart = "pair.start";
    public const string SessionCli = "session.cli";

    private static readonly SignatureAlgorithm ed25519 = SignatureAlgorithm.Ed25519;

    private readonly Key key;

    public CliKey() {
        key = Key.Create(ed25519);
    }

    public CliKey(byte[] seed) {
        key = Key.Import(ed25519, seed, KeyBlobFormat.RawPrivateKey);
    }

    public ChannelId Channel => ChannelId.FromPublicKey(key.PublicKey.Export(KeyBlobFormat.RawPublicKey));

    public string PublicKey => Base64Url.EncodeToString(key.PublicKey.Export(KeyBlobFormat.RawPublicKey));

    public string Sign(string purpose, string nonce) =>
        Base64Url.EncodeToString(ed25519.Sign(key, Encoding.UTF8.GetBytes($"jobranger relay v1\n{purpose}\n{nonce}")));

    public JsonObject Proof(string purpose, string nonce) => new() {
        ["v"] = 1,
        ["publicKey"] = PublicKey,
        ["nonce"] = nonce,
        ["signature"] = Sign(purpose, nonce),
    };

    public void Dispose() => key.Dispose();
}
