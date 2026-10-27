namespace Jobranger.Cli.Tests.Http;

internal sealed class LargeBodyStream(long length) : Stream {
    public long Served { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => length;

    public override long Position {
        get => Served;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer) {
        int count = (int)Math.Min(buffer.Length, length - Served);
        buffer[..count].Clear();
        Served += count;
        return count;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Read(buffer.Span));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Task.FromResult(Read(buffer.AsSpan(offset, count)));

    public override void Flush() {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
