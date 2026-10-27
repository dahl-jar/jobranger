using Jobranger.Cli.Catalogue;

namespace Jobranger.Cli.Features.Sources;

internal static class FeedBatch {
    private const string locationSeparator = ", ";

    public static SourceBatch Of(EmployerFeed feed, IReadOnlyList<SourceListing> listings) {
        HashSet<string> ids = new(listings.Select(listing => listing.Id), StringComparer.Ordinal);
        return new SourceBatch {
            Source = feed.Source,
            Listings = listings,
            SeenIds = ids,
            ListedIds = ids,
        };
    }

    public static string Id(EmployerFeed feed, string sourceId) => $"{feed.Source}:{sourceId}";

    public static string Location(IEnumerable<string> locations) =>
        string.Join(locationSeparator, locations.Distinct(StringComparer.Ordinal));
}
