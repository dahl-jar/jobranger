using System.IO.Pipelines;

namespace Jobranger.Relay.Hubs;

internal sealed class ActivityReader(PipeReader inner, TimeProvider time) : PipeReader
{
    private long lastReadTicks = time.GetUtcNow().UtcTicks;

    public DateTimeOffset LastRead => new(Volatile.Read(ref lastReadTicks), TimeSpan.Zero);

    public override void AdvanceTo(SequencePosition consumed) => inner.AdvanceTo(consumed);

    public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) => inner.AdvanceTo(consumed, examined);

    public override void CancelPendingRead() => inner.CancelPendingRead();

    public override void Complete(Exception? exception = null) => inner.Complete(exception);

    public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        ReadResult result = await inner.ReadAsync(cancellationToken);
        Stamp();
        return result;
    }

    public override bool TryRead(out ReadResult result)
    {
        bool read = inner.TryRead(out result);
        if (read)
        {
            Stamp();
        }

        return read;
    }

    private void Stamp() => Volatile.Write(ref lastReadTicks, time.GetUtcNow().UtcTicks);
}
