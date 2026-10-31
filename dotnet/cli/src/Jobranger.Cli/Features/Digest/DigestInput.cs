namespace Jobranger.Cli.Features.Digest;

internal sealed record DigestInput {
    public required IReadOnlyList<DigestCard> Cards { get; init; }

    public IReadOnlyList<string> CarriedIds { get; init; } = [];

    public IReadOnlyList<HiddenCount> Hidden { get; init; } = [];

    public int NewAds { get; init; }

    public IReadOnlyList<string> SourcesRead { get; init; } = [];

    public IReadOnlyList<string> FailedSources { get; init; } = [];

    public int ClosedSinceLast { get; init; }

    public bool BudgetPaused { get; init; }

    public int Pending { get; init; }

    public DigestStop? ModelStopped { get; init; }

    public double? MaxYears { get; init; }

    public required DateTimeOffset RanAt { get; init; }
}
