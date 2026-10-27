using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Runs;

public sealed record RunFinished(
    long RunId,
    string FinishedAt,
    long AdsRead,
    long NewAds,
    long Matches,
    long Hidden,
    double CostUsd,
    IReadOnlyList<string> FailedSources,
    bool BudgetPaused) : IPayload {
    public const string Tag = "run.finished";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
