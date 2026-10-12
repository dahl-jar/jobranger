using System.Text.Json.Serialization;

namespace Jobranger.Relay.Core;

internal sealed record TokenPayload(
    [property: JsonPropertyName("K")] string Kind,
    [property: JsonPropertyName("C")] string Channel,
    [property: JsonPropertyName("R")] string Role,
    [property: JsonPropertyName("E")] long Expires);
