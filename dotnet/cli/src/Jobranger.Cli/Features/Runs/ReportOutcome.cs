using Jobranger.Cli.Features.Matching;

namespace Jobranger.Cli.Features.Runs;

internal sealed record ReportOutcome(
    IReadOnlyList<string> ReportedIds,
    IReadOnlyList<string> CarriedIds,
    IReadOnlyList<string> SilentIds,
    IReadOnlyDictionary<HiddenKind, int> HiddenCounts) {
    public static readonly ReportOutcome Empty = new([], [], [], new Dictionary<HiddenKind, int>());
}
