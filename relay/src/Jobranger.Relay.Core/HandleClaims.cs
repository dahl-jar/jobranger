using Jobranger.Contracts.Channels;

namespace Jobranger.Relay.Core;

public sealed record HandleClaims(ChannelId Channel, RelayRole Role, string? Grant = null);
