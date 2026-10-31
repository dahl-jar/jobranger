namespace Jobranger.Cli.Features.Sources;

internal sealed record PageResult(string ListingId, string? Description, string? ApplyUrl, string? Label = null);
