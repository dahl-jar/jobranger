using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Jobranger.Cli.Http;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Sources;

internal sealed class FinnSource : ISource {
    public const string SourceName = "finn";

    private const string recentKey = "finn_recent_since";
    private const string searchUrl = "https://www.finn.no/job/search";
    private const string recentSort = "PUBLISHED_DESC";
    private const string occupationParameter = "occupation=";
    private const char parameterSeparator = '&';
    private const int docsPerPage = 50;
    private const int maxCategoryPages = 20;
    private const int maxRecentPages = 50;
    private const long republishedIdGap = 2_000_000;

    private static readonly Uri finnRoot = new("https://www.finn.no");
    private static readonly SourceRequestOptions options = new();

    public string Name => SourceName;

    public async Task<SourceBatch> FetchAsync(FetchContext context, CancellationToken cancellationToken) {
        Scan scan = new();
        try {
            return await ScanAsync(context, scan, cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return Batch(context, scan) with { Partial = true };
        }
    }

    private static async Task<SourceBatch> ScanAsync(FetchContext context, Scan scan, CancellationToken cancellationToken) {
        Exception? recentError = null;
        try {
            scan.Recent = await RecentAsync(context, SourceCursors.Start(context, recentKey), cancellationToken).ConfigureAwait(false);
        } catch (Exception error) when (SourceHttp.IsFailure(error, cancellationToken)) {
            LogEvents.FinnRecentFailed(context.Logger, error);
            recentError = error;
        }
        HashSet<string> known = new(context.KnownIds, StringComparer.Ordinal);
        known.UnionWith(scan.Recent?.NewListings.Select(listing => listing.Id) ?? []);
        try {
            scan.Category = await CategoryAsync(context, known, cancellationToken).ConfigureAwait(false);
        } catch (Exception error) when (SourceHttp.IsFailure(error, cancellationToken)) {
            LogEvents.FinnSearchFailed(context.Logger, error);
            throw new SourceException("finn category scan failed",
                await FinishAsync(context, scan, cancellationToken).ConfigureAwait(false), error);
        }
        if (scan.Category.Complete && scan.Category.ActiveIds.Count == 0) {
            scan.Category = null;
            throw new SourceException("finn category scan returned no ads",
                await FinishAsync(context, scan, cancellationToken).ConfigureAwait(false));
        }
        SourceBatch batch = await FinishAsync(context, scan, cancellationToken).ConfigureAwait(false);
        return recentError is null ? batch : throw new SourceException("finn recent scan failed", batch, recentError);
    }

    private static async Task<SourceBatch> FinishAsync(FetchContext context, Scan scan, CancellationToken cancellationToken) {
        await FetchPagesAsync(context, scan, cancellationToken).ConfigureAwait(false);
        return Batch(context, scan);
    }

    private static async Task<RecentScan> RecentAsync(
        FetchContext context, DateTimeOffset since, CancellationToken cancellationToken) {
        List<SourceListing> listings = [];
        Dictionary<string, string> jobTitles = new(StringComparer.Ordinal);
        for (int page = 1; page <= maxRecentPages; page++) {
            FinnSearchPage result = await SearchAsync(context.Http, $"sort={recentSort}&page={page}", cancellationToken)
                .ConfigureAwait(false);
            listings.AddRange(result.Listings);
            AddTitles(jobTitles, result.JobTitles);
            string? oldest = Oldest(listings);
            if ((oldest is not null && DateTimeOffset.Parse(oldest, CultureInfo.InvariantCulture) < since)
                || page >= result.LastPage) {
                break;
            }
        }
        HashSet<string> seen = [.. listings.Select(listing => listing.Id)];
        Dictionary<string, SourceListing> unseen = new(StringComparer.Ordinal);
        foreach (SourceListing listing in listings.Where(listing => !context.KnownIds.Contains(listing.Id))) {
            unseen[listing.Id] = listing;
        }
        List<SourceListing> wanted = [.. unseen.Values.Where(listing =>
            context.Plan.TitleMatches(jobTitles.GetValueOrDefault(listing.Id, string.Empty))
            && !IsSkippedLevel(context, TitleLevels.Of(listing.Title)))];
        return new RecentScan(FlagRepublished(wanted, seen), seen, Oldest(listings), jobTitles);
    }

    private static async Task<CategoryScan> CategoryAsync(
        FetchContext context, HashSet<string> known, CancellationToken cancellationToken) {
        string occupations = string.Join(parameterSeparator,
            context.Plan.FinnOccupations.Select(code => occupationParameter + Uri.EscapeDataString(code)));
        Dictionary<string, SourceListing> fresh = new(StringComparer.Ordinal);
        HashSet<string> active = new(StringComparer.Ordinal);
        Dictionary<string, string> jobTitles = new(StringComparer.Ordinal);
        bool complete = false;
        for (int page = 1; page <= maxCategoryPages; page++) {
            FinnSearchPage result = await SearchAsync(context.Http, $"{occupations}&page={page}", cancellationToken)
                .ConfigureAwait(false);
            AddTitles(jobTitles, result.JobTitles);
            foreach (SourceListing listing in result.Listings) {
                active.Add(listing.Id);
                if (!known.Contains(listing.Id)) {
                    fresh[listing.Id] = listing;
                }
            }
            if (page * docsPerPage >= result.MatchCount || page >= result.LastPage) {
                complete = true;
                break;
            }
        }
        return new CategoryScan(FlagRepublished([.. fresh.Values], active), active, complete, jobTitles);
    }

    private static async Task<FinnSearchPage> SearchAsync(HttpClient client, string query, CancellationToken cancellationToken) {
        byte[] body = await SourceHttp.GetAsync(client, new Uri($"{searchUrl}?{query}"), options, cancellationToken)
            .ConfigureAwait(false);
        return FinnPages.ParseSearch(Encoding.UTF8.GetString(body));
    }

    private static async Task FetchPagesAsync(FetchContext context, Scan scan, CancellationToken cancellationToken) {
        Dictionary<string, SourceListing> targets = new(StringComparer.Ordinal);
        foreach (SourceListing listing in context.PendingPages.Concat(Listings(scan))
            .Where(listing => listing.Source == SourceName)) {
            targets[listing.Id] = listing;
        }
        foreach (SourceListing listing in targets.Values) {
            string? level = TitleLevels.Of(listing.Title);
            scan.Pages.Add(IsSkippedLevel(context, level)
                ? new PageResult(listing.Id, null, null, level)
                : await PageAsync(context, listing, cancellationToken).ConfigureAwait(false));
        }
    }

    private static async Task<PageResult> PageAsync(FetchContext context, SourceListing listing, CancellationToken cancellationToken) {
        if (!Uri.TryCreate(listing.Url, UriKind.Absolute, out Uri? url) || !SourceRequests.IsSameOrigin(url, finnRoot)) {
            return new PageResult(listing.Id, null, null);
        }
        try {
            byte[] body = await SourceHttp.GetAsync(context.Http, url, options, cancellationToken).ConfigureAwait(false);
            string html = Encoding.UTF8.GetString(body);
            return new PageResult(listing.Id, FinnPages.ParseDescription(html), FinnPages.ParseApplyUrl(html));
        } catch (Exception error) when (SourceHttp.IsFailure(error, cancellationToken)) {
            LogEvents.FinnAdTextFailed(context.Logger, listing.Id, error);
            return new PageResult(listing.Id, null, null);
        }
    }

    private static bool IsSkippedLevel(FetchContext context, string? level) =>
        level is not null && context.Plan.SkipTitleLevels.Contains(level);

    private static string? Oldest(IEnumerable<SourceListing> listings) =>
        listings.Select(listing => listing.Published).OfType<string>().Min(StringComparer.Ordinal);

    private static IReadOnlyList<SourceListing> FlagRepublished(IReadOnlyList<SourceListing> listings, IReadOnlySet<string> ids) {
        long[] numeric = [.. ids.Select(id => NumericId(id[FinnPages.IdPrefix.Length..])).OfType<long>()];
        if (numeric.Length == 0) {
            return listings;
        }
        long cutoff = numeric.Max() - republishedIdGap;
        return [.. listings.Select(listing => NumericId(listing.SourceId) < cutoff ? listing with { Republished = true } : listing)];
    }

    private static long? NumericId(string text) =>
        text.Length > 0 && text.All(char.IsAsciiDigit) && long.TryParse(text, CultureInfo.InvariantCulture, out long id)
            ? id
            : null;

    private static IEnumerable<SourceListing> Listings(Scan scan) =>
        (scan.Recent?.NewListings ?? []).Concat(scan.Category?.NewListings ?? []);

    private static SourceBatch Batch(FetchContext context, Scan scan) {
        IReadOnlySet<string> recentIds = scan.Recent?.SeenIds ?? new HashSet<string>();
        IReadOnlySet<string> categoryIds = scan.Category?.ActiveIds ?? new HashSet<string>();
        return new SourceBatch {
            Source = SourceName,
            Listings = [.. Listings(scan)],
            SeenIds = new HashSet<string>(recentIds.Concat(categoryIds), StringComparer.Ordinal),
            Cursors = scan.Recent is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { [recentKey] = SourceCursors.Advanced(context.RunStarted) },
            FinnSync = new FinnSync(
                categoryIds,
                recentIds,
                scan.Recent?.WindowStart,
                [.. (scan.Recent?.NewListings ?? []).Select(listing => listing.Id)],
                scan.Category?.Complete ?? false),
            Pages = [.. scan.Pages],
            JobTitles = JobTitles(scan),
        };
    }

    private static Dictionary<string, string> JobTitles(Scan scan) {
        Dictionary<string, string> jobTitles = new(StringComparer.Ordinal);
        AddTitles(jobTitles, scan.Recent?.JobTitles);
        AddTitles(jobTitles, scan.Category?.JobTitles);
        return jobTitles;
    }

    private static void AddTitles(Dictionary<string, string> target, IReadOnlyDictionary<string, string>? titles) {
        foreach ((string id, string jobTitle) in titles ?? ReadOnlyDictionary<string, string>.Empty) {
            target[id] = jobTitle;
        }
    }

    private sealed record RecentScan(
        IReadOnlyList<SourceListing> NewListings,
        IReadOnlySet<string> SeenIds,
        string? WindowStart,
        IReadOnlyDictionary<string, string> JobTitles);

    private sealed record CategoryScan(
        IReadOnlyList<SourceListing> NewListings,
        IReadOnlySet<string> ActiveIds,
        bool Complete,
        IReadOnlyDictionary<string, string> JobTitles);

    private sealed class Scan {
        public RecentScan? Recent { get; set; }

        public CategoryScan? Category { get; set; }

        public List<PageResult> Pages { get; } = [];
    }
}
