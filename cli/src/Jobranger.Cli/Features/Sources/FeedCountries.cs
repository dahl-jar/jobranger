using System.Collections.Frozen;

namespace Jobranger.Cli.Features.Sources;

internal static class FeedCountries {
    private static readonly FrozenSet<string> norwayNames = FrozenSet.Create(StringComparer.Ordinal, "norway", "norge");

    public static bool IsNorway(string? country) =>
        country is not null && norwayNames.Contains(country.Trim().ToLowerInvariant());
}
