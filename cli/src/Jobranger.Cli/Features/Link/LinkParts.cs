namespace Jobranger.Cli.Features.Link;

internal sealed record LinkParts(IDeviceStore Devices, RelayApiClient Api, IRelayConnector Connector);
