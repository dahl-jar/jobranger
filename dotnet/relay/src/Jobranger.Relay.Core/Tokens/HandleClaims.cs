using Jobranger.Messaging.Channels;

namespace Jobranger.Relay.Core.Tokens;

public sealed record HandleClaims(ChannelId Channel, RelayRole Role, string? Grant = null);
