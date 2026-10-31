using System.Globalization;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Features.Delivery;

internal sealed class TimedStream(Stream inner, TimeSpan limit, TimeProvider time) : ForwardingStream(inner) {
    public bool TimedOut { get; private set; }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        using CancellationTokenSource timer = new(limit, time);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        try {
            return await Inner.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
        } catch (OperationCanceledException error) when (timer.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            throw Stalled(error);
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
        using CancellationTokenSource timer = new(limit, time);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        try {
            await Inner.WriteAsync(buffer, linked.Token).ConfigureAwait(false);
        } catch (OperationCanceledException error) when (timer.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            throw Stalled(error);
        }
    }

    private TimeoutException Stalled(OperationCanceledException error) {
        TimedOut = true;
        return new TimeoutException(
            string.Create(CultureInfo.InvariantCulture, $"no bytes moved for {limit.TotalSeconds} s"), error);
    }
}
