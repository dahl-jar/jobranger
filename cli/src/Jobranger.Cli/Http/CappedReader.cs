using System.Buffers;

namespace Jobranger.Cli.Http;

internal static class CappedReader {
    private const int chunkBytes = 81920;

    public static async Task<byte[]> ReadAsync(Stream body, long maxBytes, CancellationToken cancellationToken) {
        ArrayBufferWriter<byte> buffer = new();
        byte[] chunk = new byte[chunkBytes];
        int read;
        while ((read = await body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0) {
            if (buffer.WrittenCount + (long)read > maxBytes) {
                throw new BodyTooLargeException(maxBytes);
            }
            buffer.Write(chunk.AsSpan(0, read));
        }
        return buffer.WrittenSpan.ToArray();
    }
}
