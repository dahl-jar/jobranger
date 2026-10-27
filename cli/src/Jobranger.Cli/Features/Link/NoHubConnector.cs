namespace Jobranger.Cli.Features.Link;

internal sealed class NoHubConnector : IRelayConnector {
    public Task<IRelayConnection> ConnectAsync(string handle, CancellationToken cancellationToken) =>
        throw new RelayUnavailableException("relay hub client is not part of this build");
}
