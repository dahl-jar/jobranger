namespace Jobranger.Cli.Config;

internal sealed record ModelConfig {
    private const string defaultOllamaUrl = "http://localhost:11434";

    public ProviderName? Provider { get; init; }

    public string? Model { get; init; }

    public string OllamaUrl { get; init; } = defaultOllamaUrl;

    public decimal? MonthlyLimitUsd { get; init; }

    public PriceOverride? Price { get; init; }
}
