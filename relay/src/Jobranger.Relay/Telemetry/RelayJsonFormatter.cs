using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Jobranger.Relay.Telemetry;

internal sealed class RelayJsonFormatter() : ConsoleFormatter(Name) {
    public new const string Name = "relay-json";

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter) {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer)) {
            writer.WriteStartObject();
            writer.WriteNumber("EventId", logEntry.EventId.Id);
            writer.WriteString("LogLevel", logEntry.LogLevel.ToString());
            writer.WriteString("Category", logEntry.Category);
            writer.WriteString("Message", logEntry.Formatter(logEntry.State, logEntry.Exception));
            if (logEntry.Exception is { } exception) {
                writer.WriteString("Exception", exception.ToString());
            }

            if (logEntry.State is IReadOnlyCollection<KeyValuePair<string, object?>> fields) {
                writer.WriteStartObject("State");
                foreach (KeyValuePair<string, object?> field in fields) {
                    writer.WriteString(field.Key, Convert.ToString(field.Value, System.Globalization.CultureInfo.InvariantCulture));
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        textWriter.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
