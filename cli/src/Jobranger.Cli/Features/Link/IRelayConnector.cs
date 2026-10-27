namespace Jobranger.Cli.Features.Link;

internal interface IRelayConnector {
    Task<IRelayConnection> ConnectAsync(string handle, CancellationToken cancellationToken);
}
