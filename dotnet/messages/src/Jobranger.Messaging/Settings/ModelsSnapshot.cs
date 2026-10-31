using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Settings;

public sealed record ModelsSnapshot(IReadOnlyList<ProviderModels> Providers) : PayloadBase(Tag) {
    public const string Tag = "models.snapshot";
}
