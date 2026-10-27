using Jobranger.Messaging.Channels;

namespace Jobranger.Relay.Endpoints;

internal readonly record struct ProofResult(ChannelId Channel, RelayProblem? Problem);
