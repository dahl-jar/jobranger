using System.Text.Json.Serialization;

namespace Jobranger.Relay.Core;

[JsonSerializable(typeof(TokenPayload))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
