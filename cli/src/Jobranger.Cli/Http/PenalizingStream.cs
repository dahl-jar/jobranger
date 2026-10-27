namespace Jobranger.Cli.Http;

internal sealed class PenalizingStream(Stream inner, Action penalize) : Stream {
    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        try {
            return await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        } catch (IOException) {
            penalize();
            throw;
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush() {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing) {
        if (disposing) {
            inner.Dispose();
        }
        base.Dispose(disposing);
    }
}
