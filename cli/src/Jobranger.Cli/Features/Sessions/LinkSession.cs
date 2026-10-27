using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using Jobranger.Contracts.Fields;
using Jobranger.Contracts.Limits;

namespace Jobranger.Cli.Features.Sessions;

internal sealed class LinkSession : IDisposable {
    private const byte cliToBrowser = 0x01;
    private const byte browserToCli = 0x02;
    private const long seqLimit = 1L << 32;
    private const int nonceBytes = 12;
    private const int seqOffset = 4;
    private const int seqBytes = 8;

    private readonly byte[] sid;
    private readonly AesGcm sendCipher;
    private readonly AesGcm receiveCipher;
    private readonly Lock frameLock = new();
    private long lastSent;
    private long lastReceived;
    private bool closed;

    internal LinkSession(
        ReadOnlySpan<byte> sid,
        ReadOnlySpan<byte> sendKey,
        ReadOnlySpan<byte> receiveKey,
        long lastSent = 0,
        long lastReceived = 0) {
        this.sid = sid.ToArray();
        sendCipher = new AesGcm(sendKey, ContractLimits.GcmTagBytes);
        receiveCipher = new AesGcm(receiveKey, ContractLimits.GcmTagBytes);
        this.lastSent = lastSent;
        this.lastReceived = lastReceived;
    }

    public (long Seq, string Ciphertext) Seal(ReadOnlySpan<byte> plaintext) {
        lock (frameLock) {
            if (closed) {
                throw new FrameException(FrameFailure.Closed);
            }
            long seq = lastSent + 1;
            if (seq >= seqLimit) {
                throw Reject(FrameFailure.Seq);
            }
            lastSent = seq;
            byte[] output = new byte[plaintext.Length + ContractLimits.GcmTagBytes];
            sendCipher.Encrypt(
                Nonce(seq),
                plaintext,
                output.AsSpan(0, plaintext.Length),
                output.AsSpan(plaintext.Length),
                Aad(cliToBrowser, seq));
            return (seq, Base64Url.EncodeToString(output));
        }
    }

    public byte[] Open(long seq, string ciphertext) {
        lock (frameLock) {
            if (closed) {
                throw new FrameException(FrameFailure.Closed);
            }
            if (seq != lastReceived + 1 || seq >= seqLimit) {
                throw Reject(FrameFailure.Seq);
            }
            if (ciphertext.Length > ContractLimits.CiphertextB64MaxChars
                || !CanonicalBase64Url.TryDecode(ciphertext, out byte[] input)
                || input.Length < ContractLimits.GcmTagBytes) {
                throw Reject(FrameFailure.Size);
            }
            lastReceived = seq;
            int textLength = input.Length - ContractLimits.GcmTagBytes;
            byte[] output = new byte[textLength];
            try {
                receiveCipher.Decrypt(
                    Nonce(seq),
                    input.AsSpan(0, textLength),
                    input.AsSpan(textLength),
                    output,
                    Aad(browserToCli, seq));
            } catch (AuthenticationTagMismatchException) {
                throw Reject(FrameFailure.Tag);
            }
            return output;
        }
    }

    public void Dispose() {
        lock (frameLock) {
            closed = true;
            sendCipher.Dispose();
            receiveCipher.Dispose();
        }
    }

    private static byte[] Nonce(long seq) {
        byte[] nonce = new byte[nonceBytes];
        BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(seqOffset), (ulong)seq);
        return nonce;
    }

    private byte[] Aad(byte direction, long seq) {
        byte[] seqPart = new byte[seqBytes];
        BinaryPrimitives.WriteUInt64BigEndian(seqPart, (ulong)seq);
        return [.. SessionSecrets.Label, .. sid, direction, .. seqPart];
    }

    private FrameException Reject(FrameFailure reason) {
        closed = true;
        return new FrameException(reason);
    }
}
