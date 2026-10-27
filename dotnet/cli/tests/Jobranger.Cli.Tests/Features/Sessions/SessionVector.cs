using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jobranger.Cli.Tests.Mocks;
using NSec.Cryptography;

namespace Jobranger.Cli.Tests.Features.Sessions;

internal static class SessionVector {
    public const string CliStaticLabel = "jobranger vector cli static";
    public const string CliEphemeralLabel = "jobranger vector cli ephemeral";
    public const string SidLabel = "jobranger vector sid";
    public const string NonceLabel = "jobranger vector nonce";

    private const string browserStaticLabel = "jobranger vector browser static";
    private const string browserEphemeralLabel = "jobranger vector browser ephemeral";
    private const string fixturePath = "TestData/Messages/crypto/session-v1.json";
    private const string wordsField = "words";
    private const int shortValueBytes = 16;

    private static readonly JsonElement root = Load();

    public static string Text(string field) => root.GetProperty(field).GetString()!;

    public static byte[] Bytes(string field) => Base64Url.DecodeFromChars(Text(field));

    public static long Number(string field) => root.GetProperty(field).GetInt64();

    public static string[] Words() => [.. root.GetProperty(wordsField).EnumerateArray().Select(word => word.GetString()!)];

    public static Key PrivateKey(string label) =>
        Key.Import(KeyAgreementAlgorithm.X25519, LabelHash(label), KeyBlobFormat.RawPrivateKey);

    public static byte[] ShortValue(string label) => LabelHash(label)[..shortValueBytes];

    public static FakeBrowserPeer Peer() => new(
        PrivateKey(browserStaticLabel),
        PrivateKey(browserEphemeralLabel),
        ShortValue(SidLabel),
        ShortValue(NonceLabel),
        Text("agent"));

    private static byte[] LabelHash(string label) => SHA256.HashData(Encoding.ASCII.GetBytes(label));

    private static JsonElement Load() {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, fixturePath)));
        return document.RootElement.Clone();
    }
}
