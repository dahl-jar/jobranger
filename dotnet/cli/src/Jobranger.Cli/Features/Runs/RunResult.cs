using Jobranger.Cli.Features.Matching;

namespace Jobranger.Cli.Features.Runs;

internal sealed record RunResult {
    public long? RunId { get; init; }

    public required RunStatus Status { get; init; }

    public int AdsRead { get; init; }

    public int NewAds { get; init; }

    public IReadOnlyList<string> FailedSources { get; init; } = [];

    public IReadOnlyList<string> SourcesRead { get; init; } = [];

    public IReadOnlyList<string> ReportedIds { get; init; } = [];

    public IReadOnlyList<string> CarriedIds { get; init; } = [];

    public IReadOnlyList<string> SilentIds { get; init; } = [];

    public IReadOnlyDictionary<HiddenKind, int> HiddenCounts { get; init; } = new Dictionary<HiddenKind, int>();

    public int Pending { get; init; }

    public StopReason? ModelStopped { get; init; }

    public bool ModelSkipped { get; init; }

    public bool BudgetPaused => ModelStopped == StopReason.Budget;

    public bool UnpricedLimit { get; init; }

    public int UnknownEvidenceDropped { get; init; }

    public string? Version { get; init; }

    public static RunResult Locked() => new() { Status = RunStatus.Locked };
}
