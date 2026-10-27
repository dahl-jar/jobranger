using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jobranger.Cli.Features.Sessions;
using NSec.Cryptography;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeBrowserPeer : IDisposable {
    private const string label = "jobranger v1";
    private const byte cliToBrowser = 0x01;
    private const byte browserToCli = 0x02;
    private const int keyBytes = 32;
    private const int randomBytes = 16;
    private const int gcmNonceBytes = 12;
    private const int seqOffset = 4;
    private const int seqBytes = 8;
    private const int tagBytes = 16;
    private const int sharedSecrets = 3;
    private const int thirdSecretStart = keyBytes * 2;

    private static readonly KeyAgreementAlgorithm x25519 = KeyAgreementAlgorithm.X25519;

    private readonly Key staticKey;
    private readonly Key ephemeralKey;
    private readonly byte[] sid;
    private readonly byte[] nonceB;
    private byte[] cliStatic = [];
    private byte[] cliEph = [];
    private SessionSecrets? secrets;
    private long sentSeq;

    public FakeBrowserPeer()
        : this(
            Key.Create(x25519),
            Key.Create(x25519),
            RandomNumberGenerator.GetBytes(randomBytes),
            RandomNumberGenerator.GetBytes(randomBytes),
            DeviceFixtures.BrowserName) {
    }

    public FakeBrowserPeer(Key staticKey, Key ephemeralKey, byte[] sid, byte[] nonceB, string agent) {
        this.staticKey = staticKey;
        this.ephemeralKey = ephemeralKey;
        this.sid = sid;
        this.nonceB = nonceB;
        Agent = agent;
        EphemeralPublic = ephemeralKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);
    }

    public string Agent { get; }

    public byte[] EphemeralPublic { get; set; }

    public string Sid => Base64Url.EncodeToString(sid);

    public byte[] StaticPublic => staticKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);

    public IReadOnlyList<string> Words => SasWords.From(Secrets.Sas, SasWords.List);

    private SessionSecrets Secrets => secrets ??= cliEph.Length == 0
        ? throw new InvalidOperationException("no offer read yet")
        : Derive();

    public static string Encode(string json) => Encode(Encoding.UTF8.GetBytes(json));

    public static string Encode(byte[] utf8) => Base64Url.EncodeToString(utf8);

    public string Commit(byte[]? pairTag = null) {
        byte[] commit = SessionSecrets.Commit(StaticPublic, EphemeralPublic, nonceB, Encoding.UTF8.GetBytes(Agent));
        return Step(writer => {
            writer.WriteString("t", "commit");
            writer.WriteString("browser_static", Base64Url.EncodeToString(StaticPublic));
            writer.WriteString("commit", Base64Url.EncodeToString(commit));
            if (pairTag is not null) {
                writer.WriteString("pair_tag", Base64Url.EncodeToString(pairTag));
            }
        });
    }

    public void ReadOffer(string offer) {
        Dictionary<string, string> fields = Fields(offer, "offer", "cli_static", "cli_eph");
        cliStatic = Base64Url.DecodeFromChars(fields["cli_static"]);
        cliEph = Base64Url.DecodeFromChars(fields["cli_eph"]);
    }

    public string Reveal(string? agent = null) => Step(writer => {
        writer.WriteString("t", "reveal");
        writer.WriteString("browser_eph", Base64Url.EncodeToString(EphemeralPublic));
        writer.WriteString("nonce_b", Base64Url.EncodeToString(nonceB));
        writer.WriteString("agent", agent ?? Agent);
    });

    public bool AcceptsCliConfirm(string confirm) {
        byte[] mac = Base64Url.DecodeFromChars(Fields(confirm, "confirm", "mac")["mac"]);
        return CryptographicOperations.FixedTimeEquals(mac, Secrets.CliMac());
    }

    public string Confirm(byte[]? mac = null) => Step(writer => {
        writer.WriteString("t", "confirm");
        writer.WriteString("mac", Base64Url.EncodeToString(mac ?? Secrets.BrowserMac()));
    });

    public (long Seq, string Ciphertext) Seal(string plaintext) {
        sentSeq++;
        byte[] input = Encoding.UTF8.GetBytes(plaintext);
        byte[] output = new byte[input.Length + tagBytes];
        using AesGcm cipher = new(Secrets.KeyB2c, tagBytes);
        cipher.Encrypt(
            Nonce(sentSeq),
            input,
            output.AsSpan(0, input.Length),
            output.AsSpan(input.Length),
            Aad(browserToCli, sentSeq));
        return (sentSeq, Base64Url.EncodeToString(output));
    }

    public string Open(long seq, string ciphertext) {
        byte[] input = Base64Url.DecodeFromChars(ciphertext);
        byte[] output = new byte[input.Length - tagBytes];
        using AesGcm cipher = new(Secrets.KeyC2b, tagBytes);
        cipher.Decrypt(Nonce(seq), input.AsSpan(0, output.Length), input.AsSpan(output.Length), output, Aad(cliToBrowser, seq));
        return Encoding.UTF8.GetString(output);
    }

    public void Dispose() {
        staticKey.Dispose();
        ephemeralKey.Dispose();
        secrets?.Dispose();
    }

    private SessionSecrets Derive() {
        byte[] agent = Encoding.UTF8.GetBytes(Agent);
        byte[] transcript = SessionSecrets.Transcript(sid, cliStatic, StaticPublic, cliEph, EphemeralPublic, nonceB, agent);
        byte[] ikm = new byte[keyBytes * sharedSecrets];
        bool agreed = SessionSecrets.TryAgree(ephemeralKey, cliEph, ikm.AsSpan(0, keyBytes))
            && SessionSecrets.TryAgree(ephemeralKey, cliStatic, ikm.AsSpan(keyBytes, keyBytes))
            && SessionSecrets.TryAgree(staticKey, cliEph, ikm.AsSpan(thirdSecretStart, keyBytes));
        return agreed ? SessionSecrets.Derive(ikm, transcript) : throw new InvalidOperationException("all-zero key");
    }

    private static string Step(Action<Utf8JsonWriter> write) {
        using MemoryStream content = new();
        using (Utf8JsonWriter writer = new(content)) {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }
        return Encode(content.ToArray());
    }

    private static Dictionary<string, string> Fields(string step, string kind, params string[] keys) {
        using JsonDocument document = JsonDocument.Parse(Base64Url.DecodeFromChars(step));
        Dictionary<string, string> fields = document.RootElement.EnumerateObject()
            .ToDictionary(field => field.Name, field => field.Value.GetString() ?? "", StringComparer.Ordinal);
        string[] expected = ["t", .. keys];
        if (fields.GetValueOrDefault("t") != kind || !fields.Keys.Order().SequenceEqual(expected.Order())) {
            throw new InvalidOperationException($"browser refuses step expected t={kind} keys={string.Join(',', expected)}");
        }
        return fields;
    }

    private static byte[] Nonce(long seq) {
        byte[] nonce = new byte[gcmNonceBytes];
        BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(seqOffset), (ulong)seq);
        return nonce;
    }

    private byte[] Aad(byte direction, long seq) {
        byte[] seqPart = new byte[seqBytes];
        BinaryPrimitives.WriteUInt64BigEndian(seqPart, (ulong)seq);
        return [.. Encoding.ASCII.GetBytes(label), .. sid, direction, .. seqPart];
    }
}
