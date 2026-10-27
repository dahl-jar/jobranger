using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Runs;

public sealed record RunProgress(
    long RunId,
    string Mode,
    string StartedAt,
    int Percent,
    IReadOnlyList<SourceStep> Sources,
    ModelStep Model) : IPayload {
    public const string Tag = "run.progress";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
