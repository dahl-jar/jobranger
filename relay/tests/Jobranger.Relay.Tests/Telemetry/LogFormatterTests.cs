using System.Text.Json.Nodes;
using Jobranger.Relay.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobranger.Relay.Tests.Telemetry;

public sealed class LogFormatterTests {
    private const string written = "pairing started channel=1:abc";

    [Fact]
    public void ShouldWriteStateFieldsWithoutStateMessage() {
        JsonObject line = Formatted(null);

        Assert.Equal(
            (written, "1:abc", false),
            ((string?)line["Message"], (string?)line["State"]?["Channel"], line["State"]!.AsObject().ContainsKey("Message")));
    }

    [Fact]
    public void ShouldWriteExceptionText() {
        JsonObject line = Formatted(new InvalidOperationException("relay key missing"));

        Assert.Contains("relay key missing", (string?)line["Exception"], StringComparison.Ordinal);
    }

    private static JsonObject Formatted(Exception? exception) {
        List<KeyValuePair<string, object?>> state = [new("Channel", "1:abc"), new("{OriginalFormat}", "pairing started channel={Channel}")];
        LogEntry<List<KeyValuePair<string, object?>>> entry = new(
            LogLevel.Information, "Jobranger.Relay", new EventId(1), state, exception, (_, _) => written);
        using StringWriter output = new();

        new RelayJsonFormatter().Write(entry, null, output);

        return JsonNode.Parse(output.ToString())!.AsObject();
    }
}
