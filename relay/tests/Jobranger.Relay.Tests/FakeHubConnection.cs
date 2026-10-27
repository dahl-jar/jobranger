using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobranger.Relay.Tests;

internal sealed class FakeHubConnection(string connectionId, bool stalls = false)
    : HubConnectionContext(new DefaultConnectionContext(connectionId), new HubConnectionContextOptions(), NullLoggerFactory.Instance) {
    public List<SerializedHubMessage> Written { get; } = [];

    public List<CancellationToken> StalledWrites { get; } = [];

    public bool Aborted { get; private set; }

    public override ValueTask WriteAsync(SerializedHubMessage message, CancellationToken cancellationToken = default) {
        if (stalls) {
            StalledWrites.Add(cancellationToken);
            return new ValueTask(Task.Delay(Timeout.Infinite, cancellationToken));
        }

        Written.Add(message);
        return ValueTask.CompletedTask;
    }

    public override void Abort() => Aborted = true;
}
