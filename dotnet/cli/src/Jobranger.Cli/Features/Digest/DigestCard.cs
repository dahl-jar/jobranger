namespace Jobranger.Cli.Features.Digest;

internal sealed record DigestCard {
    public required string Id { get; init; }

    public required string Source { get; init; }

    public required string Title { get; init; }

    public string? DisplayTitle { get; init; }

    public required string Company { get; init; }

    public required string Location { get; init; }

    public string? Deadline { get; init; }

    public DateOnly? DeadlineDate { get; init; }

    public required string Url { get; init; }

    public string? Summary { get; init; }

    public required string RoleLabel { get; init; }

    public required DigestFit Fit { get; init; }

    public bool IsStrongMatch { get; init; }

    public bool ApplyExpired { get; init; }

    public string ShownTitle => string.IsNullOrEmpty(DisplayTitle) ? Title : DisplayTitle;
}
