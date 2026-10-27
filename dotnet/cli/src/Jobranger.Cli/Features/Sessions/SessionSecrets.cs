using System.Buffers.Binary;
using System.Security.Cryptography;
using NSec.Cryptography;

namespace Jobranger.Cli.Features.Sessions;

internal sealed class SessionSecrets : IDisposable {
    public const int KeyBytes = 32;

    private const int derivedKeys = 3;
    private const int okmBytes = KeyBytes * derivedKeys;
    private const int confirmKeyStart = KeyBytes * 2;
    private const int lengthPrefixBytes = 2;

    private static readonly KeyAgreementAlgorithm x25519 = KeyAgreementAlgorithm.X25519;

    private SessionSecrets(byte[] transcriptHash, byte[] okm, byte[] sas) {
        TranscriptHash = transcriptHash;
        KeyC2b = okm[..KeyBytes];
        KeyB2c = okm[KeyBytes..confirmKeyStart];
        KeyConfirm = okm[confirmKeyStart..];
        Sas = sas;
    }

    public byte[] TranscriptHash { get; }

    public byte[] KeyC2b { get; }

    public byte[] KeyB2c { get; }

    public byte[] KeyConfirm { get; }

    public byte[] Sas { get; }

    internal static ReadOnlySpan<byte> Label => "jobranger v1"u8;

    private static ReadOnlySpan<byte> CommitLabel => "jobranger v1 commit"u8;

    private static ReadOnlySpan<byte> KeysInfo => "jobranger v1 keys"u8;

    private static ReadOnlySpan<byte> SasInfo => "jobranger v1 sas"u8;

    private static ReadOnlySpan<byte> CliRole => "cli"u8;

    private static ReadOnlySpan<byte> BrowserRole => "browser"u8;

    public static SessionSecrets Derive(ReadOnlySpan<byte> ikm, ReadOnlySpan<byte> transcriptHash) {
        byte[] okm = new byte[okmBytes];
        try {
            HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, okm, transcriptHash, KeysInfo);
            byte[] sas = new byte[SasWords.SasBytes];
            HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, sas, transcriptHash, SasInfo);
            return new SessionSecrets(transcriptHash.ToArray(), okm, sas);
        } finally {
            CryptographicOperations.ZeroMemory(okm);
        }
    }

    public static byte[] Commit(
        ReadOnlySpan<byte> browserStatic,
        ReadOnlySpan<byte> browserEph,
        ReadOnlySpan<byte> nonceB,
        ReadOnlySpan<byte> agent) =>
        SHA256.HashData([.. CommitLabel, .. browserStatic, .. browserEph, .. nonceB, .. AgentPart(agent)]);

    public static byte[] Transcript(
        ReadOnlySpan<byte> sid,
        ReadOnlySpan<byte> cliStatic,
        ReadOnlySpan<byte> browserStatic,
        ReadOnlySpan<byte> cliEph,
        ReadOnlySpan<byte> browserEph,
        ReadOnlySpan<byte> nonceB,
        ReadOnlySpan<byte> agent) =>
        SHA256.HashData([
            .. Label, .. sid, .. cliStatic, .. browserStatic, .. cliEph, .. browserEph, .. nonceB, .. AgentPart(agent),
        ]);

    public static bool TryAgree(Key privateKey, ReadOnlySpan<byte> peerPublic, Span<byte> sharedSecret) {
        if (!PublicKey.TryImport(x25519, peerPublic, KeyBlobFormat.RawPublicKey, out PublicKey? peer) || peer is null) {
            return false;
        }
        SharedSecretCreationParameters exportable = new() { ExportPolicy = KeyExportPolicies.AllowPlaintextExport };
        using SharedSecret? agreed = x25519.Agree(privateKey, peer, in exportable);
        if (agreed is null) {
            return false;
        }
        byte[] raw = agreed.Export(SharedSecretBlobFormat.RawSharedSecret);
        raw.CopyTo(sharedSecret);
        CryptographicOperations.ZeroMemory(raw);
        return true;
    }

    public byte[] CliMac() => RoleMac(CliRole);

    public byte[] BrowserMac() => RoleMac(BrowserRole);

    public void Dispose() {
        CryptographicOperations.ZeroMemory(TranscriptHash);
        CryptographicOperations.ZeroMemory(KeyC2b);
        CryptographicOperations.ZeroMemory(KeyB2c);
        CryptographicOperations.ZeroMemory(KeyConfirm);
        CryptographicOperations.ZeroMemory(Sas);
    }

    private byte[] RoleMac(ReadOnlySpan<byte> role) {
        byte[] message = [.. role, .. TranscriptHash];
        return HMACSHA256.HashData(KeyConfirm, message);
    }

    private static byte[] AgentPart(ReadOnlySpan<byte> agent) {
        byte[] part = new byte[lengthPrefixBytes + agent.Length];
        BinaryPrimitives.WriteUInt16BigEndian(part, (ushort)agent.Length);
        agent.CopyTo(part.AsSpan(lengthPrefixBytes));
        return part;
    }
}
