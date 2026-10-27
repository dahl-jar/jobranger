using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Settings;

public sealed record KeyReplace(string Id, string Provider, string Key) : IPayload {
    public const string Tag = "key.replace";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;

    public override string ToString() => $"{nameof(KeyReplace)} {{ Id = {Id}, Provider = {Provider} }}";
}
