using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Settings;

public sealed record KeyReplace(string Id, string Provider, string Key) : PayloadBase(Tag) {
    public const string Tag = "key.replace";

    public override string ToString() => $"{nameof(KeyReplace)} {{ Id = {Id}, Provider = {Provider} }}";
}
