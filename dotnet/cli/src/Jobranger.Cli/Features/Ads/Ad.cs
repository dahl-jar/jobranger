namespace Jobranger.Cli.Features.Ads;

internal sealed record Ad {
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

    public string? Label { get; init; }

    public string? Summary { get; init; }

    public bool RequiresMaster { get; init; }

    public bool? IsProgrammingJob { get; init; }

    public string? DisplayTitle { get; init; }

    public string? RoleType { get; init; }

    public IReadOnlyList<string> Technologies { get; init; } = [];

    public IReadOnlyList<string> Requirements { get; init; } = [];

    public string? Assessment { get; init; }

    public string? AssessmentVersion { get; init; }

    public string? ApplyUrl { get; init; }

    public string? ApplyStatus { get; init; }

    public string? ApplyCheckedAt { get; init; }

    public bool Republished { get; init; }

    public string? FirstSeen { get; init; }

    public string? ClosedAt { get; init; }

    public string? FirstClassifiedAt { get; init; }
}
