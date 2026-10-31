using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Jobranger.Messaging.Payloads;

internal static class PayloadSchema {
    public const string Reference = "$ref";
    public const string Properties = "properties";
    public const string Items = "items";
    public const string TagProperty = "type";

    private const string definitions = "$defs";
    private const string resourceName = "payload.schema.json";
    private const string oneOfKeyword = "oneOf";
    private const string constKeyword = "const";
    private const string patternKeyword = "pattern";
    private const string schemaKeyword = "$schema";
    private const string referencePrefix = "#/$defs/";

    private static readonly Uri baseUri = new("https://jobranger.invalid/payload.schema.json");
    private static readonly JsonObject document = Load();
    private static readonly BuildOptions buildOptions = new() { SchemaRegistry = new SchemaRegistry() };
    private static readonly JsonSchema root = JsonSchema.FromText(document.ToJsonString(), buildOptions, baseUri);
    private static readonly Dictionary<string, string> definitionsByTag = TaggedDefinitions();
    private static readonly ConcurrentDictionary<string, Lazy<JsonSchema>> schemas = new(StringComparer.Ordinal);

    public static JsonObject DefinitionNodes => document[definitions]!.AsObject();

    public static bool TryGetDefinition(string tag, [NotNullWhen(true)] out string? definition) =>
        definitionsByTag.TryGetValue(tag, out definition);

    public static JsonSchema For(string definition) =>
        schemas.GetOrAdd(definition, key => new Lazy<JsonSchema>(() => Build(key))).Value;

    public static string NameOf(string reference) => reference[referencePrefix.Length..];

    private static JsonSchema Build(string definition) {
        JsonObject wrapper = new() {
            [schemaKeyword] = document[schemaKeyword]!.DeepClone(),
            [Reference] = $"{baseUri}{referencePrefix}{definition}",
        };
        return JsonSchema.FromText(wrapper.ToJsonString(), buildOptions, new Uri(root.BaseUri, definition));
    }

    private static Dictionary<string, string> TaggedDefinitions() =>
        document[oneOfKeyword]!.AsArray()
            .Select(branch => NameOf(branch![Reference]!.GetValue<string>()))
            .ToDictionary(
                name => DefinitionNodes[name]![Properties]![TagProperty]![constKeyword]!.GetValue<string>(),
                name => name,
                StringComparer.Ordinal);

    private static JsonObject Load() {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!;
        JsonObject loaded = JsonNode.Parse(stream)!.AsObject();
        TranslatePatterns(loaded);
        return loaded;
    }

    private static void TranslatePatterns(JsonNode? node) {
        if (node is JsonArray array) {
            foreach (JsonNode? item in array) {
                TranslatePatterns(item);
            }
        } else if (node is JsonObject map) {
            foreach (string key in map.Select(entry => entry.Key).ToList()) {
                if (key == patternKeyword && map[key] is JsonValue value) {
                    map[key] = SchemaPatterns.ToDotNet(value.GetValue<string>());
                } else {
                    TranslatePatterns(map[key]);
                }
            }
        }
    }
}
