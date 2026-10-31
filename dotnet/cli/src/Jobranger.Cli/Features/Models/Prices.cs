using Jobranger.Cli.Config;

namespace Jobranger.Cli.Features.Models;

internal static class Prices {
    private const decimal cacheReadRatio = 0.1m;
    private const decimal cacheWrite5mRatio = 1.25m;
    private const decimal cacheWrite1hRatio = 2m;

    private static readonly ModelSpec free = new(0m, 0m, 0m, 0m, 0m, SupportsEffort: false);

    public static ModelSpec? SpecFor(ModelCatalog catalog, ProviderName provider, string modelId, PriceOverride? price) {
        if (provider == ProviderName.Ollama) {
            return free;
        }
        ModelSpec? table = catalog.Find(provider, modelId);
        if (price is null) {
            return table;
        }
        if (table is null) {
            return new ModelSpec(
                price.InputPerMtok,
                price.OutputPerMtok,
                price.InputPerMtok * cacheReadRatio,
                price.InputPerMtok * cacheWrite5mRatio,
                price.InputPerMtok * cacheWrite1hRatio,
                SupportsEffort: false);
        }
        decimal scale = price.InputPerMtok / table.Input;
        return table with {
            Input = price.InputPerMtok,
            Output = price.OutputPerMtok,
            CacheRead = table.CacheRead * scale,
            CacheWrite5m = table.CacheWrite5m * scale,
            CacheWrite1h = table.CacheWrite1h * scale,
        };
    }
}
