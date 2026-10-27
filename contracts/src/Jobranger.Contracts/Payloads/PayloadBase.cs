using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;

namespace Jobranger.Contracts.Payloads;

public abstract record PayloadBase : IPayload {
    protected PayloadBase(string type) {
        Type = type;
    }

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type { get; }

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
