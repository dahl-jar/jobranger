using System.Collections.Frozen;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jobranger.Cli.Config;

namespace Jobranger.Cli.Features.Models;

internal sealed partial class ModelCatalog(FrozenDictionary<string, ModelSpec> specs) {
    private const string resourceName = "Jobranger.Cli.Features.Models.prices.json";
    private const char keySeparator = ':';

    public static ModelCatalog Embedded { get; } = Load();

    public static ModelCatalog Parse(string json) {
        using JsonDocument document = JsonDocument.Parse(json);
        Dictionary<string, ModelSpec> specs = new(StringComparer.Ordinal);
        foreach (JsonProperty entry in document.RootElement.EnumerateObject()) {
            JsonElement spec = entry.Value;
            specs[entry.Name] = new ModelSpec(
                spec.GetProperty("input").GetDecimal(),
                spec.GetProperty("output").GetDecimal(),
                spec.GetProperty("cache_read").GetDecimal(),
                spec.GetProperty("cache_write_5m").GetDecimal(),
                spec.GetProperty("cache_write_1h").GetDecimal(),
                spec.GetProperty("supports_effort").GetBoolean());
        }
        return new ModelCatalog(specs.ToFrozenDictionary(StringComparer.Ordinal));
    }

    public ModelSpec? Find(ProviderName provider, string modelId) {
        string prefix = WireNames.Of(provider) + keySeparator;
        return specs.GetValueOrDefault(prefix + modelId)
            ?? specs.GetValueOrDefault(prefix + DateSuffix().Replace(modelId, string.Empty));
    }

    private static ModelCatalog Load() {
        using Stream stream = typeof(ModelCatalog).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"embedded resource {resourceName} is missing");
        using StreamReader reader = new(stream);
        return Parse(reader.ReadToEnd());
    }

    [GeneratedRegex(@"-\d{8}$")]
    private static partial Regex DateSuffix();
}
