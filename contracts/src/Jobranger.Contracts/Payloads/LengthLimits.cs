using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Jobranger.Contracts.Payloads;

internal static class LengthLimits {
    private const string anyOf = "anyOf";
    private const string type = "type";
    private const string maxLength = "maxLength";
    private const string maxItems = "maxItems";
    private const string nullType = "null";
    private const string booleanType = "boolean";
    private const string stringType = "string";
    private const string arrayType = "array";
    private const string objectType = "object";
    private const string numberType = "number";
    private const string integerType = "integer";

    private static readonly ConcurrentDictionary<JsonObject, JsonObject> resolvedNodes = new(ReferenceEqualityComparer.Instance);

    public static SchemaFailure? Find(JsonElement instance, JsonObject node, string path) {
        JsonObject resolved = Resolve(node);
        if (resolved[anyOf] is JsonArray branches) {
            return FindInBranches(instance, branches, path);
        }
        return instance.ValueKind switch {
            JsonValueKind.String when CodePoints(instance.GetString()!) > Limit(resolved, maxLength) => new SchemaFailure(path, maxLength),
            JsonValueKind.Array => FindInArray(instance, resolved, path),
            JsonValueKind.Object => FindInObject(instance, resolved, path),
            _ => null,
        };
    }

    private static SchemaFailure? FindInBranches(JsonElement instance, JsonArray branches, string path) {
        List<SchemaFailure?> found = [.. branches
            .Select(branch => Resolve(branch!.AsObject()))
            .Where(branch => MatchesType(instance, branch))
            .Select(branch => Find(instance, branch, path))];
        return found.Count == 0 || found.Contains(null) ? null : found[0];
    }

    private static SchemaFailure? FindInObject(JsonElement instance, JsonObject node, string path) {
        if (node[PayloadSchema.Properties] is not JsonObject properties) {
            return null;
        }
        foreach ((string key, JsonNode? child) in properties) {
            if (instance.TryGetProperty(key, out JsonElement value)) {
                SchemaFailure? found = Find(value, child!.AsObject(), $"{path}.{key}");
                if (found is not null) {
                    return found;
                }
            }
        }
        return null;
    }

    private static SchemaFailure? FindInArray(JsonElement instance, JsonObject node, string path) {
        if (instance.GetArrayLength() > Limit(node, maxItems)) {
            return new SchemaFailure(path, maxItems);
        }
        if (node[PayloadSchema.Items] is not JsonObject itemNode) {
            return null;
        }
        int index = 0;
        foreach (JsonElement item in instance.EnumerateArray()) {
            SchemaFailure? found = Find(item, itemNode, $"{path}[{index}]");
            if (found is not null) {
                return found;
            }
            index++;
        }
        return null;
    }

    private static bool MatchesType(JsonElement instance, JsonObject node) {
        if (node[type] is not JsonValue kind) {
            return true;
        }
        return (kind.GetValue<string>(), instance.ValueKind) switch {
            (nullType, JsonValueKind.Null) => true,
            (booleanType, JsonValueKind.True or JsonValueKind.False) => true,
            (stringType, JsonValueKind.String) => true,
            (arrayType, JsonValueKind.Array) => true,
            (objectType, JsonValueKind.Object) => true,
            (numberType, JsonValueKind.Number) => true,
            (integerType, JsonValueKind.Number) => instance.TryGetInt64(out _),
            _ => false,
        };
    }

    private static JsonObject Resolve(JsonObject node) => resolvedNodes.GetOrAdd(node, Merge);

    private static JsonObject Merge(JsonObject node) {
        JsonObject resolved = node;
        while (resolved[PayloadSchema.Reference] is JsonValue reference) {
            JsonObject merged = PayloadSchema.DefinitionNodes[PayloadSchema.NameOf(reference.GetValue<string>())]!.DeepClone().AsObject();
            foreach ((string key, JsonNode? value) in resolved.Where(entry => entry.Key != PayloadSchema.Reference)) {
                merged[key] = value?.DeepClone();
            }
            resolved = merged;
        }
        return resolved;
    }

    private static long Limit(JsonObject node, string keyword) => node[keyword] is JsonValue limit ? limit.GetValue<long>() : long.MaxValue;

    private static int CodePoints(string text) => text.Length - text.Count(char.IsLowSurrogate);
}
