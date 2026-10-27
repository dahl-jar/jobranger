using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Jobranger.Cli.Features.Sessions;

internal static class SasWords {
    public const int SasBytes = 8;

    private const string resourceName = "Jobranger.Cli.Features.Sessions.wordlist.txt";
    private const string listSha256 = "7AA57A4D3ECF6581729992BAD9575BACDEBF7C28378AF2AEC6A50F11AEC326F5";
    private const int wordCount = 4;
    private const char lineEnd = '\n';

    private static readonly Lazy<IReadOnlyList<string>> list = new(Load);

    public static IReadOnlyList<string> List => list.Value;

    public static IReadOnlyList<string> From(ReadOnlySpan<byte> sas, IReadOnlyList<string> words) {
        ulong remaining = BinaryPrimitives.ReadUInt64BigEndian(sas);
        ulong size = (ulong)words.Count;
        string[] picked = new string[wordCount];
        for (int index = 0; index < wordCount; index++) {
            picked[index] = words[(int)(remaining % size)];
            remaining /= size;
        }
        return picked;
    }

    public static IReadOnlyList<string> Read(Stream stream) {
        using MemoryStream content = new();
        stream.CopyTo(content);
        byte[] bytes = content.ToArray();
        string actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (actual != listSha256) {
            throw new InvalidOperationException($"word list sha256 {actual} differs from pinned {listSha256}");
        }
        return Encoding.UTF8.GetString(bytes).Split(lineEnd, StringSplitOptions.RemoveEmptyEntries);
    }

    private static IReadOnlyList<string> Load() {
        using Stream stream = typeof(SasWords).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("embedded resource missing name=" + resourceName);
        return Read(stream);
    }
}
