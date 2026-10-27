using Jobranger.Contracts.Channels;

namespace Jobranger.Relay.Endpoints;

internal readonly record struct ProofResult(ChannelId Channel, RelayProblem? Problem);
