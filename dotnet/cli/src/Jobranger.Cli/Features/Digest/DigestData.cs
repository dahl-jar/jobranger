namespace Jobranger.Cli.Features.Digest;

internal sealed record DigestData {
    public required DateOnly Day { get; init; }

    public required DateTimeOffset RanAt { get; init; }

    public IReadOnlyList<DigestCard> ApplySoon { get; init; } = [];

    public IReadOnlyList<DigestCard> NewToday { get; init; } = [];

    public IReadOnlyList<DigestCard> Carried { get; init; } = [];

    public IReadOnlyList<HiddenCount> Hidden { get; init; } = [];

    public int NewAds { get; init; }

    public IReadOnlyList<string> SourcesRead { get; init; } = [];

    public int EmployerPages { get; init; }

    public IReadOnlyList<string> FailedSources { get; init; } = [];

    public int ClosedSinceLast { get; init; }

    public bool BudgetPaused { get; init; }

    public int Pending { get; init; }

    public double? MaxYears { get; init; }

    public DigestStop? ModelStopped { get; init; }
}
