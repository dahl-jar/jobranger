using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Runs;

public sealed record Status(
    string CliVersion,
    long ContractVersion,
    string Machine,
    string PairedAt,
    LastRun? LastRun,
    string? NextRunAt,
    ActiveRun? Running,
    bool BudgetPaused) : IPayload {
    public const string Tag = "status";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
