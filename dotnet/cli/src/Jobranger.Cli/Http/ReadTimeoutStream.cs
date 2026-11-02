using System.Globalization;

namespace Jobranger.Cli.Http;

internal sealed class ReadTimeoutStream(Stream inner, TimeSpan timeout, TimeProvider time) : ForwardingStream(inner) {
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        using CancellationTokenSource deadline = new(timeout, time);
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try {
            return await Inner.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
        } catch (OperationCanceledException) when (deadline.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested) {
            throw new IOException(string.Create(
                CultureInfo.InvariantCulture, $"no bytes from source for {timeout.TotalSeconds} s"));
        }
    }
}
