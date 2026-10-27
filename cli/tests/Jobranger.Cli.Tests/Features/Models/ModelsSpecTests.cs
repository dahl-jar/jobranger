using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Models;
using static Jobranger.Cli.Tests.Mocks.ProviderFixtures;
using ModelList = Jobranger.Cli.Features.Models.Models;

namespace Jobranger.Cli.Tests.Features.Models;

public sealed class ModelsSpecTests {
    private const string unknownModel = "vendor/unknown-model";

    [Fact]
    public void ShouldReadTablePrices_WhenModelIsListed() {
        ModelSpec? spec = ModelList.SpecFor(ProviderName.Anthropic, Sonnet, null);

        Assert.Equal(new ModelSpec(2m, 10m, 0.1m, 2.5m, 4m, SupportsEffort: true), spec);
    }

    [Fact]
    public void ShouldReadUndatedEntry_WhenModelIdHasDateSuffix() {
        ModelSpec? spec = ModelList.SpecFor(ProviderName.Anthropic, "claude-haiku-4-5-20251001", null);

        Assert.Equal((1m, 5m, false), (spec?.Input, spec?.Output, spec?.SupportsEffort));
    }

    [Fact]
    public void ShouldPreferExactDatedEntry_WhenBothExist() {
        ModelCatalog catalog = ModelCatalog.Parse(
            """{"anthropic:claude-x-20260101": {"input": 1, "output": 2, "cache_read": 0.1, "cache_write_5m": 1.25, "cache_write_1h": 2, "supports_effort": false},"""
            + """ "anthropic:claude-x": {"input": 3, "output": 4, "cache_read": 0.3, "cache_write_5m": 3.75, "cache_write_1h": 6, "supports_effort": false}}""");

        ModelSpec? spec = catalog.Find(ProviderName.Anthropic, "claude-x-20260101");

        Assert.Equal(1m, spec?.Input);
    }

    [Fact]
    public void ShouldLeaveUnknownModelUnpriced_WhenNoOverrideIsSet() {
        Assert.Null(ModelList.SpecFor(ProviderName.OpenRouter, unknownModel, null));
    }

    [Fact]
    public void ShouldPriceOllamaAtZero_WhenAnyModelIsChosen() {
        ModelSpec? spec = ModelList.SpecFor(ProviderName.Ollama, "llama3.2", new PriceOverride(4m, 20m));

        Assert.Equal(new ModelSpec(0m, 0m, 0m, 0m, 0m, SupportsEffort: false), spec);
    }

    [Fact]
    public void ShouldScaleCachePrices_WhenOverrideChangesListedModel() {
        ModelSpec? spec = ModelList.SpecFor(ProviderName.Anthropic, Sonnet, new PriceOverride(4m, 20m));

        Assert.Equal(new ModelSpec(4m, 20m, 0.2m, 5m, 8m, SupportsEffort: true), spec);
    }

    [Fact]
    public void ShouldDeriveCachePrices_WhenOverrideNamesUnknownModel() {
        ModelSpec? spec = ModelList.SpecFor(ProviderName.OpenRouter, unknownModel, new PriceOverride(2m, 8m));

        Assert.Equal(new ModelSpec(2m, 8m, 0.2m, 2.5m, 4m, SupportsEffort: false), spec);
    }
}
