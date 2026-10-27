using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jobranger.Cli.Tests.Mocks;

[JsonSerializable(typeof(JsonElement))]
internal sealed partial class RelayTestJsonContext : JsonSerializerContext;
