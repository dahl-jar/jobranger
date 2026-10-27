using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Settings;

public sealed record ModelsSnapshot(IReadOnlyList<ProviderModels> Providers) : PayloadBase(Tag) {
    public const string Tag = "models.snapshot";
}
