using System.Globalization;

namespace Jobranger.Cli.Http;

internal sealed class ReadTimeoutStream(Stream inner, TimeSpan timeout, TimeProvider time) : Stream {
    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => inner.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        using CancellationTokenSource deadline = new(timeout, time);
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try {
            return await inner.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
        } catch (OperationCanceledException) when (deadline.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested) {
            throw new IOException(string.Create(
                CultureInfo.InvariantCulture, $"no bytes from source for {timeout.TotalSeconds} s"));
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        inner.WriteAsync(buffer, cancellationToken);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        inner.WriteAsync(buffer, offset, count, cancellationToken);

    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override void Flush() => inner.Flush();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing) {
        if (disposing) {
            inner.Dispose();
        }
        base.Dispose(disposing);
    }
}
