using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Http;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Sources;

internal sealed partial class NavSource : ISource {
    private const string sourceName = "nav";
    private const string positionKey = "nav_position";
    private const int maxPages = 200;
    private const int matchTimeoutMs = 1000;
    private const string idPrefix = "nav:";
    private const string activeStatus = "ACTIVE";
    private const string styrkType = "STYRK08";
    private const string authorizationHeader = "Authorization";
    private const string bearerPrefix = "Bearer ";
    private const string entryPath = "/api/v1/feedentry/";
    private const string locationSeparator = ", ";
    private const string itemsKey = "items";
    private const string feedUrlKey = "feed_url";
    private const string nextUrlKey = "next_url";
    private const string feedEntryKey = "_feed_entry";
    private const string uuidKey = "uuid";
    private const string statusKey = "status";
    private const string adContentKey = "ad_content";
    private const string occupationCategoriesKey = "occupationCategories";
    private const string level1Key = "level1";
    private const string level2Key = "level2";
    private const string categoryListKey = "categoryList";
    private const string categoryTypeKey = "categoryType";
    private const string codeKey = "code";
    private const string titleKey = "title";
    private const string jobTitleKey = "jobtitle";
    private const string employerKey = "employer";
    private const string nameKey = "name";
    private const string workLocationsKey = "workLocations";
    private const string cityKey = "city";
    private const string municipalKey = "municipal";
    private const string publishedKey = "published";
    private const string expiresKey = "expires";
    private const string linkKey = "link";
    private const string sourceUrlKey = "sourceurl";
    private const string descriptionKey = "description";
    private const string applicationUrlKey = "applicationUrl";

    private static readonly Uri apiBase = new("https://pam-stilling-feed.nav.no");
    private static readonly Uri tokenUrl = new(apiBase, "/api/publicToken");
    private static readonly Uri lastPageUrl = new(apiBase, "/api/v1/feed?last=true");

    public string Name => sourceName;

    public async Task<SourceBatch> FetchAsync(FetchContext context, CancellationToken cancellationToken) {
        Progress progress = new(context);
        SourceRequestOptions options;
        try {
            options = await AuthorizeAsync(context.Http, cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return progress.Batch();
        } catch (Exception error) when (SourceHttp.IsFailure(error, cancellationToken)) {
            LogEvents.NavTokenFailed(context.Logger, error);
            throw new SourceException("nav token failed", null, error);
        }
        try {
            await ReadFeedAsync(context, options, progress, cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return progress.Batch();
        } catch (Exception error) when (SourceHttp.IsFailure(error, cancellationToken)) {
            LogEvents.NavFeedFailed(context.Logger, error);
            throw new SourceException("nav feed failed", progress.Batch(), error);
        }
        return progress.Batch();
    }

    private static async Task<SourceRequestOptions> AuthorizeAsync(HttpClient client, CancellationToken cancellationToken) {
        byte[] body = await SourceHttp.GetAsync(client, tokenUrl, new SourceRequestOptions(), cancellationToken)
            .ConfigureAwait(false);
        Match token = JwtPattern().Match(Encoding.UTF8.GetString(body));
        if (!token.Success) {
            throw new InvalidDataException("no JWT in publicToken response");
        }
        return new SourceRequestOptions {
            Headers = new Dictionary<string, string> { [authorizationHeader] = bearerPrefix + token.Value },
        };
    }

    private static async Task ReadFeedAsync(
        FetchContext context, SourceRequestOptions options, Progress progress, CancellationToken cancellationToken) {
        Uri? url = context.Cursors.TryGetValue(positionKey, out string? stored)
            ? OnApiHost(stored) ?? lastPageUrl
            : lastPageUrl;
        HashSet<string> visited = new(StringComparer.Ordinal);
        for (int page = 0; url is not null && page < maxPages; page++) {
            if (!visited.Add(url.AbsoluteUri)) {
                throw new InvalidDataException("nav feed page repeats");
            }
            byte[] body = await SourceHttp.GetAsync(context.Http, url, options, cancellationToken).ConfigureAwait(false);
            FeedPage feed = ParsePage(body, url);
            progress.Cursor = feed.FeedUrl.AbsoluteUri;
            if (feed.Skipped > 0) {
                LogEvents.NavItemsSkipped(context.Logger, feed.Skipped);
            }
            foreach ((string uuid, bool active) in feed.Entries) {
                await ReadEntryAsync(context, options, progress, uuid, active, cancellationToken).ConfigureAwait(false);
            }
            url = feed.NextUrl;
        }
    }

    private static async Task ReadEntryAsync(
        FetchContext context, SourceRequestOptions options, Progress progress, string uuid, bool active,
        CancellationToken cancellationToken) {
        string id = idPrefix + uuid;
        progress.Seen.Add(id);
        progress.Active[id] = active;
        if (!active || !progress.Fetched.Add(id)) {
            return;
        }
        SourceListing? listing;
        try {
            Uri url = new(apiBase, entryPath + Uri.EscapeDataString(uuid));
            byte[] body = await SourceHttp.GetAsync(context.Http, url, options, cancellationToken).ConfigureAwait(false);
            listing = Listing(uuid, body, context.Plan);
        } catch (Exception error) when (SourceHttp.IsFailure(error, cancellationToken)) {
            LogEvents.NavEntryFailed(context.Logger, id, error);
            return;
        }
        if (listing is not null) {
            progress.Listings.Add(listing);
        }
    }

    private static FeedPage ParsePage(byte[] body, Uri url) {
        using JsonDocument document = JsonFields.Parse(body);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) {
            throw new InvalidDataException("nav feed page is not an object");
        }
        List<(string Uuid, bool Active)> entries = [];
        int skipped = 0;
        if (root.TryGetProperty(itemsKey, out JsonElement items)) {
            if (items.ValueKind != JsonValueKind.Array) {
                throw new InvalidDataException("nav feed items is not a list");
            }
            foreach (JsonElement item in items.EnumerateArray()) {
                if (JsonFields.Object(item, feedEntryKey) is JsonElement entry
                    && JsonFields.FilledText(entry, uuidKey) is string uuid && Guid.TryParse(uuid, out _)) {
                    entries.Add((uuid, JsonFields.Text(entry, statusKey) == activeStatus));
                } else {
                    skipped++;
                }
            }
        }
        return new FeedPage(Link(root, feedUrlKey) ?? url, Link(root, nextUrlKey), entries, skipped);
    }

    private static Uri? Link(JsonElement page, string key) {
        if (!page.TryGetProperty(key, out JsonElement value) || value.ValueKind == JsonValueKind.Null) {
            return null;
        }
        if (JsonFields.Text(page, key) is not string text) {
            throw new InvalidDataException($"nav feed {key} is not text");
        }
        if (text.Length == 0) {
            return null;
        }
        return OnApiHost(text) ?? throw new InvalidDataException($"nav feed {key} leaves the API host");
    }

    private static Uri? OnApiHost(string text) =>
        Uri.TryCreate(apiBase, text, out Uri? url) && SourceRequests.IsSameOrigin(url, apiBase) ? url : null;

    private static SourceListing? Listing(string uuid, byte[] body, SourcePlan plan) {
        using JsonDocument document = JsonFields.Parse(body);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) {
            throw new InvalidDataException("nav entry is not an object");
        }
        return JsonFields.Object(root, adContentKey) is JsonElement content && IsDeveloperAd(content, plan)
            ? ToListing(uuid, content)
            : null;
    }

    private static bool IsDeveloperAd(JsonElement content, SourcePlan plan) =>
        JsonFields.Objects(content, occupationCategoriesKey).Any(category =>
            plan.NavCategories.Contains((Lowered(category, level1Key), Lowered(category, level2Key))))
        || JsonFields.Objects(content, categoryListKey).Any(category =>
            JsonFields.Text(category, categoryTypeKey) == styrkType
            && JsonFields.Text(category, codeKey) is string code
            && plan.NavStyrk.Any(prefix => code.StartsWith(prefix, StringComparison.Ordinal)))
        || plan.TitleMatches(JsonFields.Text(content, titleKey) ?? string.Empty)
        || plan.TitleMatches(JsonFields.Text(content, jobTitleKey) ?? string.Empty);

    private static string Lowered(JsonElement category, string key) =>
        (JsonFields.Text(category, key) ?? string.Empty).Trim().ToLowerInvariant();

    private static SourceListing ToListing(string uuid, JsonElement content) => new() {
        Id = idPrefix + uuid,
        Source = sourceName,
        SourceId = uuid,
        Title = JsonFields.Text(content, titleKey) ?? string.Empty,
        Company = JsonFields.Object(content, employerKey) is JsonElement employer
            ? JsonFields.Text(employer, nameKey) ?? string.Empty
            : string.Empty,
        Location = Location(content),
        Published = JsonFields.FilledText(content, publishedKey),
        Deadline = JsonFields.FilledText(content, expiresKey),
        Url = JsonFields.FilledText(content, linkKey) ?? JsonFields.FilledText(content, sourceUrlKey) ?? string.Empty,
        Description = HtmlText.Strip(JsonFields.Text(content, descriptionKey) ?? string.Empty) is { Length: > 0 } text
            ? text
            : null,
        ApplyUrl = JsonFields.FilledText(content, applicationUrlKey),
    };

    private static string Location(JsonElement content) =>
        string.Join(locationSeparator, JsonFields.Objects(content, workLocationsKey)
            .SelectMany(Places)
            .Select(part => part?.Trim())
            .OfType<string>()
            .Where(part => part.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase));

    private static IEnumerable<string?> Places(JsonElement place) =>
        [JsonFields.Text(place, cityKey), JsonFields.Text(place, municipalKey)];

    [GeneratedRegex("eyJ[A-Za-z0-9_.-]+", RegexOptions.None, matchTimeoutMs)]
    private static partial Regex JwtPattern();

    private sealed record FeedPage(Uri FeedUrl, Uri? NextUrl, IReadOnlyList<(string Uuid, bool Active)> Entries, int Skipped);

    private sealed class Progress(FetchContext context) {
        public List<SourceListing> Listings { get; } = [];

        public HashSet<string> Seen { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Fetched { get; } = new(context.KnownIds, StringComparer.Ordinal);

        public Dictionary<string, bool> Active { get; } = new(StringComparer.Ordinal);

        public string? Cursor { get; set; } = context.Cursors.GetValueOrDefault(positionKey);

        public SourceBatch Batch() => new() {
            Source = sourceName,
            Listings = [.. Listings],
            SeenIds = new HashSet<string>(Seen, StringComparer.Ordinal),
            Cursors = Cursor is null ? new Dictionary<string, string>() : new Dictionary<string, string> { [positionKey] = Cursor },
            Active = new Dictionary<string, bool>(Active, StringComparer.Ordinal),
        };
    }
}
