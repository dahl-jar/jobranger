namespace Jobranger.Cli.Features.Sources;

internal sealed record SourceBatch {
    public required string Source { get; init; }

    public IReadOnlyList<SourceListing> Listings { get; init; } = [];

    public IReadOnlySet<string> SeenIds { get; init; } = new HashSet<string>();

    public int Read => SeenIds.Count;

    public IReadOnlyDictionary<string, string> Cursors { get; init; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, bool> Active { get; init; } = new Dictionary<string, bool>();

    public FinnSync? FinnSync { get; init; }

    public IReadOnlyList<PageResult> Pages { get; init; } = [];
}
