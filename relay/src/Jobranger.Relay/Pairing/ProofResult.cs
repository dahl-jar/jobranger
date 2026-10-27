using Jobranger.Contracts.Channels;
using Jobranger.Relay.Core;

namespace Jobranger.Relay.Pairing;

internal readonly record struct ProofResult(ChannelId Channel, RelayProblem? Problem);
