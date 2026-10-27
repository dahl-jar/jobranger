using Jobranger.Messaging.Envelopes;

namespace Jobranger.Cli.Features.Link;

internal interface IRelayConnection : IAsyncDisposable {
    Task SendAsync(Envelope envelope, CancellationToken cancellationToken);

    ValueTask<RelayEvent> ReceiveAsync(CancellationToken cancellationToken);
}
