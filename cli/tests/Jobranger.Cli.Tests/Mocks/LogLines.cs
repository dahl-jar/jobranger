using System.Text.Json.Nodes;

namespace Jobranger.Cli.Tests.Mocks;

internal static class LogLines {
    public static IReadOnlyList<JsonObject> Read(string path) =>
        [.. File.ReadAllLines(path).Select(line => JsonNode.Parse(line)!.AsObject())];

    public static IReadOnlyList<KeyValuePair<string, object?>> Fields(params (string Key, object? Value)[] fields) =>
        [.. fields.Select(field => new KeyValuePair<string, object?>(field.Key, field.Value))];
}
