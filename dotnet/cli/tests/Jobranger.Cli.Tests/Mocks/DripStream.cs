using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class DripStream(FakeTimeProvider time, TimeSpan gap) : MemoryStream {
    private const byte dripByte = (byte)'x';

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        time.Advance(gap);
        cancellationToken.ThrowIfCancellationRequested();
        buffer.Span[0] = dripByte;
        return ValueTask.FromResult(1);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
}
