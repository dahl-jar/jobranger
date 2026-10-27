using System.Text.Json.Serialization;

namespace Jobranger.Relay.Core;

public sealed record Presence(
    [property: JsonPropertyName("cli_online")] bool CliOnline,
    [property: JsonPropertyName("browsers")] int Browsers);
