using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Runs;

public sealed record UsageSnapshot(
    string Month,
    long MonthTokens,
    long TodayTokens,
    double MonthUsd,
    double TodayUsd,
    string ResetsOn,
    IReadOnlyList<UsageDay> Days) : IPayload {
    public const string Tag = "usage.snapshot";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
