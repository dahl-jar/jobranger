using System.Text.Json;
using System.Text.Json.Serialization;
using Jobranger.Relay.Core.Channels;
using Jobranger.Relay.Endpoints;
using Jobranger.Relay.Hubs;

namespace Jobranger.Relay;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(NonceRequest))]
[JsonSerializable(typeof(NonceResponse))]
[JsonSerializable(typeof(ProofRequest))]
[JsonSerializable(typeof(PairStartResponse))]
[JsonSerializable(typeof(ClaimRequest))]
[JsonSerializable(typeof(WebSessionResponse))]
[JsonSerializable(typeof(CliSessionResponse))]
[JsonSerializable(typeof(TicketRequest))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Presence))]
[JsonSerializable(typeof(SessionsClosed))]
internal sealed partial class RelayJsonContext : JsonSerializerContext;
