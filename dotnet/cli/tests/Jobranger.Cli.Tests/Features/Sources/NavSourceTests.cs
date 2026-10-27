using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Testing;
using static Jobranger.Cli.Tests.Mocks.FakeSiteHandler;
using static Jobranger.Cli.Tests.Mocks.SourceFixtures;

namespace Jobranger.Cli.Tests.Features.Sources;

public sealed class NavSourceTests {
    private const string positionKey = "nav_position";
    private const string tokenPath = "/api/publicToken";
    private const string entryPath = "/api/v1/feedentry/";
    private const string firstPage = "/api/v1/feed/1";
    private const string secondPage = "/api/v1/feed/2";
    private const string lastPageUrl = NavBase + "/api/v1/feed?last=true";
    private const string otherHost = "evil.example.test";
    private const long hostileBodyBytes = 11L * 1024 * 1024;
    private const int pageCap = 200;
    private const int hostileUuidChars = 70_000;

    private static readonly NavSource source = new();

    [Theory]
    [InlineData("occupationCategories", """[{"level1": "IT", "level2": "Utvikling"}]""")]
    [InlineData("title", "\"Systemutvikler\"")]
    [InlineData("jobtitle", "\"Systemutvikler\"")]
    [InlineData("categoryList", """[{"categoryType": "STYRK08", "code": "2512"}]""")]
    public async Task ShouldKeepAd_WhenDeveloperSignalMatches(string key, string value) {
        FakeSiteHandler handler = Site(_ => Page([FeedItem(NavUuid)]), NavEntry((key, JsonNode.Parse(value))));

        SourceBatch batch = await source.FetchAsync(Context(handler, plan: BasePlan), CancellationToken.None);

        Assert.Equal([NavId], batch.Listings.Select(listing => listing.Id));
    }

    [Theory]
    [InlineData("title", "\"Barne- og ungdomsarbeider\"")]
    [InlineData("occupationCategories", """[{"level1": "IT", "level2": "Drift"}]""")]
    [InlineData("occupationCategories", "\"IT\"")]
    [InlineData("categoryList", """[{"categoryType": "STYRK08", "code": "2521"}]""")]
    [InlineData("categoryList", """[{"categoryType": "JANZZ", "code": "2512"}]""")]
    [InlineData("categoryList", "\"2512\"")]
    public async Task ShouldDropAd_WhenNoDeveloperSignalMatches(string key, string value) {
        FakeSiteHandler handler = Site(_ => Page([FeedItem(NavUuid)]), NavEntry((key, JsonNode.Parse(value))));

        SourceBatch batch = await source.FetchAsync(Context(handler, plan: BasePlan), CancellationToken.None);

        Assert.Empty(batch.Listings);
    }

    [Fact]
    public async Task ShouldMapEntryFields_WhenEntryIsDeveloperWork() {
        FakeSiteHandler handler = Site(_ => Page([FeedItem(NavUuid)]), DeveloperEntry());

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        SourceListing listing = Assert.Single(batch.Listings);
        Assert.Equal(new SourceListing {
            Id = NavId,
            Source = "nav",
            SourceId = NavUuid,
            Title = "Utvikler",
            Company = "Org One kommune",
            Location = "BERGEN",
            Published = "2026-08-24T00:00:00+02:00",
            Deadline = "2026-09-07T00:00:00+02:00",
            Url = "https://arbeidsplassen.nav.no/stillinger/stilling/00000000-0000-4000-8000-000000000001",
            ApplyUrl = "https://jobs.example.com/apply/1001?channel=NavRest&iso=no",
        }, listing with { Description = null });
        Assert.StartsWith("Vil du jobbe med barn og unge? Teamet bygger", listing.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldListCityAndMunicipality_WhenTheyDiffer() {
        JsonObject entry = NavEntry(
            ("title", "Utvikler"),
            ("workLocations", JsonNode.Parse("""[{"city": "LANGHUS", "municipal": "NORDRE FOLLO"}]""")));
        FakeSiteHandler handler = Site(_ => Page([FeedItem(NavUuid)]), entry);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal("LANGHUS, NORDRE FOLLO", Assert.Single(batch.Listings).Location);
    }

    [Theory]
    [InlineData("""[{"city": "OSLO", "municipal": "OSLO"}, {"city": "TRONDHEIM", "municipal": "TRONDHEIM"}]""",
        "OSLO, TRONDHEIM")]
    [InlineData("""[{"city": null, "municipal": null}, {"city": "Oslo"}]""", "Oslo")]
    public async Task ShouldListEveryWorkLocation_WhenAdNamesSeveral(string workLocations, string location) {
        JsonObject entry = NavEntry(("title", "Utvikler"), ("workLocations", JsonNode.Parse(workLocations)));
        FakeSiteHandler handler = Site(_ => Page([FeedItem(NavUuid)]), entry);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(location, Assert.Single(batch.Listings).Location);
    }

    [Theory]
    [InlineData("""[{"city": " ", "municipal": "NORDRE FOLLO"}]""", "NORDRE FOLLO")]
    [InlineData("null", "")]
    public async Task ShouldListLocation_WhenPartsAreBlankOrMissing(string workLocations, string location) {
        JsonObject entry = NavEntry(("title", "Utvikler"), ("workLocations", JsonNode.Parse(workLocations)));
        FakeSiteHandler handler = Site(_ => Page([FeedItem(NavUuid)]), entry);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(location, Assert.Single(batch.Listings).Location);
    }

    [Fact]
    public async Task ShouldNotFetchEntry_WhenIdIsKnown() {
        FakeSiteHandler handler = Site(_ => Page([FeedItem(NavUuid)]), DeveloperEntry());

        SourceBatch batch = await source.FetchAsync(
            Context(handler, known: new HashSet<string> { NavId }), CancellationToken.None);

        Assert.Equal((0, 0), (EntryUrls(handler).Count, batch.Listings.Count));
    }

    [Fact]
    public async Task ShouldFetchEntryOnce_WhenUuidRepeats() {
        FakeSiteHandler handler = Site(_ => Page([FeedItem(NavUuid), FeedItem(NavUuid)]), DeveloperEntry());

        await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Single(EntryUrls(handler));
    }

    [Fact]
    public async Task ShouldCountUniqueListings_WhenUuidRepeats() {
        FakeSiteHandler handler = Site(_ => Page([FeedItem(NavUuid), FeedItem(NavUuid), FeedItem(SecondNavUuid, "INACTIVE")]),
            DeveloperEntry());

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(2, batch.Read);
    }

    [Fact]
    public async Task ShouldRecordActiveFlags_WhenEntriesAreActiveOrInactive() {
        FakeSiteHandler handler = Site(_ => Page([FeedItem(NavUuid, "INACTIVE"), FeedItem(SecondNavUuid)]), DeveloperEntry());

        SourceBatch batch = await source.FetchAsync(
            Context(handler, known: new HashSet<string> { SecondNavId }), CancellationToken.None);

        Assert.Equal(
            new Dictionary<string, bool> { [NavId] = false, [SecondNavId] = true },
            batch.Active);
    }

    [Fact]
    public async Task ShouldStoreAbsoluteFeedUrl_WhenPageNamesRelativeOne() {
        FakeSiteHandler handler = Site(_ => Page([FeedItem(NavUuid)], feedUrl: firstPage), DeveloperEntry());

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(NavBase + firstPage, batch.Cursors[positionKey]);
    }

    [Fact]
    public async Task ShouldStartAtStoredCursor_WhenCursorIsRelative() {
        FakeSiteHandler handler = Site(_ => Page([]), DeveloperEntry());
        Dictionary<string, string> cursors = new() { [positionKey] = "/api/v1/feed/stored" };

        await source.FetchAsync(Context(handler, cursors: cursors), CancellationToken.None);

        Assert.Equal([new Uri(NavBase + "/api/v1/feed/stored")], FeedUrls(handler));
    }

    [Fact]
    public async Task ShouldStartAtLastPage_WhenStoredCursorIsOnOtherHost() {
        FakeSiteHandler handler = Site(_ => Page([]), DeveloperEntry());
        Dictionary<string, string> cursors = new() { [positionKey] = $"https://{otherHost}/api/v1/feed/1" };

        await source.FetchAsync(Context(handler, cursors: cursors), CancellationToken.None);

        Assert.Equal([new Uri(lastPageUrl)], FeedUrls(handler));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    public async Task ShouldStopPagingQuietly_WhenNextUrlIsEmpty(string nextUrl) {
        FakeSiteHandler handler = Site(_ => Body($$"""{"items": [], "next_url": {{nextUrl}}}"""), DeveloperEntry());

        await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Single(FeedUrls(handler));
    }

    [Fact]
    public async Task ShouldFollowNextUrl_WhenPageNamesOne() {
        FakeSiteHandler handler = Site(
            url => url.AbsolutePath == secondPage ? Page([]) : Page([], nextUrl: secondPage), DeveloperEntry());

        await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal([new Uri(lastPageUrl), new Uri(NavBase + secondPage)], FeedUrls(handler));
    }

    [Fact]
    public async Task ShouldKeepStoredCursor_WhenFeedFails() {
        FakeSiteHandler handler = Site(_ => Status(HttpStatusCode.NotFound), DeveloperEntry());
        string stored = NavBase + "/api/v1/feed/stored";
        Dictionary<string, string> cursors = new() { [positionKey] = stored };

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler, cursors: cursors), CancellationToken.None));

        Assert.Equal(stored, error.Partial?.Cursors[positionKey]);
    }

    [Fact]
    public async Task ShouldSkipItem_WhenFeedEntryOrUuidIsMissing() {
        JsonObject noEntry = new() { ["other"] = 1 };
        JsonObject noUuid = new() { ["_feed_entry"] = new JsonObject { ["status"] = "ACTIVE" } };
        FakeSiteHandler handler = Site(_ => Page([noEntry, noUuid]), DeveloperEntry());
        FakeLogger logger = new();

        SourceBatch batch = await source.FetchAsync(Context(handler, logger: logger), CancellationToken.None);

        Assert.Equal((0, 0, "2"), (EntryUrls(handler).Count, batch.Active.Count, SkippedItems(logger)));
    }

    [Fact]
    public async Task ShouldSkipItem_WhenUuidIsNotGuid() {
        FakeSiteHandler handler = Site(_ => Page([FeedItem(new string('a', hostileUuidChars)), FeedItem(NavUuid)]),
            DeveloperEntry());
        FakeLogger logger = new();

        SourceBatch batch = await source.FetchAsync(Context(handler, logger: logger), CancellationToken.None);

        Assert.Equal((1, NavId, "1"), (EntryUrls(handler).Count, Ids(batch.Listings), SkippedItems(logger)));
    }

    [Fact]
    public async Task ShouldSendBearerToken_WhenReadingFeedAndEntries() {
        FakeSiteHandler handler = Site(_ => Page([FeedItem(NavUuid)]), DeveloperEntry());

        await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(["Bearer " + NavToken, "Bearer " + NavToken], handler.Authorizations.Skip(1));
    }

    [Fact]
    public async Task ShouldFailWithoutPartial_WhenTokenIsMissing() {
        FakeSiteHandler handler = new(url => url.AbsolutePath == tokenPath ? Body("no token here") : Page([]));

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.Null(error.Partial);
    }

    [Theory]
    [InlineData("token", "nav.token.failed")]
    [InlineData("feed", "nav.feed.failed")]
    public async Task ShouldLogFailure_WhenNavStageFails(string stage, string eventName) {
        FakeSiteHandler handler = new(url => url.AbsolutePath == tokenPath && stage == "token"
            ? Status(HttpStatusCode.InternalServerError)
            : url.AbsolutePath == tokenPath ? Body(NavToken)
            : Status(HttpStatusCode.InternalServerError));
        FakeLogger logger = new();

        await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler, logger: logger), CancellationToken.None));

        Assert.Equal(eventName, Events(logger));
    }

    [Theory]
    [InlineData("html")]
    [InlineData("list")]
    [InlineData("items object")]
    [InlineData("huge body")]
    [InlineData("lone surrogate next url")]
    [InlineData("number next url")]
    public async Task ShouldKeepPartialBatch_WhenNavPageIsMalformed(string hostile) {
        FakeSiteHandler handler = Site(
            url => url.AbsolutePath == secondPage
                ? HostilePage(hostile)
                : Page([FeedItem(NavUuid)], feedUrl: firstPage, nextUrl: secondPage),
            DeveloperEntry());

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.Equal(
            (NavId, NavBase + firstPage),
            (Ids(error.Partial!.Listings), error.Partial.Cursors[positionKey]));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("list")]
    [InlineData("latin1")]
    [InlineData("server error")]
    public async Task ShouldSkipEntry_WhenNavEntryIsUnreadable(string hostile) {
        FakeSiteHandler handler = new(url => url.AbsolutePath switch {
            tokenPath => Body(NavToken),
            entryPath + NavUuid => HostileEntry(hostile),
            entryPath + SecondNavUuid => Body(DeveloperEntry().ToJsonString()),
            _ => Page([FeedItem(NavUuid), FeedItem(SecondNavUuid)]),
        });
        FakeLogger logger = new();

        SourceBatch batch = await source.FetchAsync(Context(handler, logger: logger), CancellationToken.None);

        Assert.Equal(
            (SecondNavId, "nav.entry.failed"),
            (Ids(batch.Listings), Events(logger)));
    }

    [Fact]
    public async Task ShouldKeepOtherAds_WhenNavTitleIsLoneSurrogate() {
        FakeSiteHandler handler = new(url => url.AbsolutePath switch {
            tokenPath => Body(NavToken),
            entryPath + NavUuid => Body("""{"ad_content": {"title": "\ud800"}}"""),
            entryPath + SecondNavUuid => Body(DeveloperEntry().ToJsonString()),
            _ => Page([FeedItem(NavUuid), FeedItem(SecondNavUuid)]),
        });

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(SecondNavId, Ids(batch.Listings));
    }

    [Fact]
    public async Task ShouldStopPaging_WhenNavUrlRepeats() {
        FakeSiteHandler handler = Site(_ => Page([], feedUrl: firstPage, nextUrl: firstPage), DeveloperEntry());

        await Assert.ThrowsAsync<SourceException>(() => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.Equal(2, FeedUrls(handler).Count);
    }

    [Fact]
    public async Task ShouldStopPaging_WhenPageCapIsReached() {
        int page = 0;
        FakeSiteHandler handler = Site(_ => Page([], nextUrl: $"/api/v1/feed/{++page}"), DeveloperEntry());

        await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(pageCap, FeedUrls(handler).Count);
    }

    [Fact]
    public async Task ShouldNotSendToken_WhenNextUrlIsOnOtherHost() {
        FakeSiteHandler handler = Site(
            _ => Page([], feedUrl: firstPage, nextUrl: $"https://{otherHost}{secondPage}"), DeveloperEntry());

        await Assert.ThrowsAsync<SourceException>(() => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.DoesNotContain(handler.Urls, url => url.Host == otherHost);
    }

    [Fact]
    public async Task ShouldKeepCursorOnApiHost_WhenFeedUrlIsOnOtherHost() {
        FakeSiteHandler handler = Site(_ => Page([FeedItem(NavUuid)], feedUrl: $"https://{otherHost}{firstPage}"),
            DeveloperEntry());

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.Empty(error.Partial!.Cursors);
    }

    private static JsonObject DeveloperEntry() => NavEntry(("title", "Utvikler"));

    private static HttpResponseMessage Page(JsonArray items, string? feedUrl = null, string? nextUrl = null) =>
        Body(FeedPage(items, feedUrl, nextUrl));

    private static FakeSiteHandler Site(Func<Uri, HttpResponseMessage> feed, JsonObject entry) =>
        new(url => url.AbsolutePath == tokenPath ? Body($$"""{"publicToken": "{{NavToken}}"}""")
            : url.AbsolutePath.StartsWith(entryPath, StringComparison.Ordinal) ? Body(entry.ToJsonString())
            : feed(url));

    private static List<Uri> EntryUrls(FakeSiteHandler handler) =>
        [.. handler.Urls.Where(url => url.AbsolutePath.StartsWith(entryPath, StringComparison.Ordinal))];

    private static List<Uri> FeedUrls(FakeSiteHandler handler) =>
        [.. handler.Urls.Where(url => url.AbsolutePath.StartsWith("/api/v1/feed", StringComparison.Ordinal)
            && !url.AbsolutePath.StartsWith(entryPath, StringComparison.Ordinal))];

    private static HttpResponseMessage HostilePage(string hostile) => hostile switch {
        "html" => Body("<html><body>Vedlikehold</body></html>"),
        "list" => Body("[]"),
        "items object" => Body("""{"items": {}}"""),
        "lone surrogate next url" => Body("""{"items": [], "next_url": "\ud800"}"""),
        "number next url" => Body("""{"items": [], "next_url": 5}"""),
        _ => Body(new byte[hostileBodyBytes]),
    };

    private static HttpResponseMessage HostileEntry(string hostile) => hostile switch {
        "not json" => Body("{bad"),
        "list" => Body("[]"),
        "latin1" => Body(Encoding.Latin1.GetBytes("""{"ad_content": {"title": "Utvikler på Ås"}}""")),
        _ => Status(HttpStatusCode.InternalServerError),
    };

    private static string? SkippedItems(FakeLogger logger) =>
        Assert.Single(logger.Collector.GetSnapshot(), record => record.Id.Name == "nav.feed.skipped")
            .GetStructuredStateValue("Skipped");
}
