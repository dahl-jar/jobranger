using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jobranger.Cli.Features.Link;

[JsonSerializable(typeof(JsonElement))]
internal sealed partial class LinkJsonContext : JsonSerializerContext;
