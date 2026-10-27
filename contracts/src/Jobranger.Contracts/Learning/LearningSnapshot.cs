using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Learning;

public sealed record LearningSnapshot(
    string State,
    string? UnavailableReason,
    long DecisionCount,
    int ColdStartThreshold,
    IReadOnlyList<LearningExample> Examples,
    long ExamplesTotal) : IPayload {
    public const string Tag = "learning.snapshot";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
