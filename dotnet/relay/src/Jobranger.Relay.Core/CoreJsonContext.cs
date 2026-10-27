using System.Text.Json.Serialization;
using Jobranger.Relay.Core.Tokens;

namespace Jobranger.Relay.Core;

[JsonSerializable(typeof(TokenPayload))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
