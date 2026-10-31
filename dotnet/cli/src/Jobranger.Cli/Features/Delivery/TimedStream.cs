using System.Globalization;

namespace Jobranger.Cli.Features.Delivery;

internal sealed class TimedStream(Stream inner, TimeSpan limit, TimeProvider time) : Stream {
    public bool TimedOut { get; private set; }

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => inner.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        using CancellationTokenSource timer = new(limit, time);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        try {
            return await inner.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
        } catch (OperationCanceledException error) when (timer.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            throw Stalled(error);
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
        using CancellationTokenSource timer = new(limit, time);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        try {
            await inner.WriteAsync(buffer, linked.Token).ConfigureAwait(false);
        } catch (OperationCanceledException error) when (timer.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            throw Stalled(error);
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing) {
        if (disposing) {
            inner.Dispose();
        }
        base.Dispose(disposing);
    }

    private TimeoutException Stalled(OperationCanceledException error) {
        TimedOut = true;
        return new TimeoutException(
            string.Create(CultureInfo.InvariantCulture, $"no bytes moved for {limit.TotalSeconds} s"), error);
    }
}
