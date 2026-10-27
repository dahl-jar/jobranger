namespace Jobranger.Cli.Features.Sources;

internal sealed record SourceListing {
    public required string Id { get; init; }

    public required string Source { get; init; }

    public required string SourceId { get; init; }

    public required string Title { get; init; }

    public required string Company { get; init; }

    public required string Location { get; init; }

    public string? Published { get; init; }

    public string? Deadline { get; init; }

    public required string Url { get; init; }

    public string? Description { get; init; }

    public string? ApplyUrl { get; init; }

    public bool Republished { get; init; }
}
