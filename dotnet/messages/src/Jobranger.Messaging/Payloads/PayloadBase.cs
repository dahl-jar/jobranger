using System.Text.Json.Serialization;
using Jobranger.Messaging.Limits;

namespace Jobranger.Messaging.Payloads;

public abstract record PayloadBase : IPayload {
    protected PayloadBase(string type) {
        Type = type;
    }

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type { get; }

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => MessageLimits.ContractVersion;
}
