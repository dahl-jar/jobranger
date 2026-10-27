namespace Jobranger.Cli.Features.Sources;

internal sealed record FinnSync(
    IReadOnlySet<string> CategoryIds,
    IReadOnlySet<string> RecentIds,
    string? WindowStart,
    IReadOnlyList<string> UnlistedIds,
    bool Complete);
