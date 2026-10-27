using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jobranger.Relay.Tests.Mocks;

[JsonSerializable(typeof(JsonElement))]
internal sealed partial class HubTestJsonContext : JsonSerializerContext;
