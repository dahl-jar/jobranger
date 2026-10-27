namespace Jobranger.Contracts.Settings;

public sealed record ProviderModels(string Provider, string? Error, IReadOnlyList<ModelInfo> Models);
