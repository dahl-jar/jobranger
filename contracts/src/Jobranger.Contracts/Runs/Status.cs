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
    bool BudgetPaused) : PayloadBase(Tag) {
    public const string Tag = "status";
}
