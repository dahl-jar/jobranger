using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Jobranger.Cli.Features.Digest;

namespace Jobranger.Cli.Features.Delivery;

internal static class IdempotencyKey {
    private const int lengthBytes = sizeof(ulong);

    public static string For(long? runId, RenderedDigest rendered, EmailAddresses addresses) {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        string run = runId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        foreach (string part in new[] { run, rendered.Subject, rendered.Text, rendered.Html, addresses.Sender, addresses.To }) {
            Append(hash, part);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string part) {
        byte[] bytes = Encoding.UTF8.GetBytes(part);
        Span<byte> length = stackalloc byte[lengthBytes];
        BinaryPrimitives.WriteUInt64BigEndian(length, (ulong)bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
