using System.Net;
using System.Text.Json.Nodes;
using System.Web;
using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Testing;
using static Jobranger.Cli.Tests.Mocks.FakeSiteHandler;
using static Jobranger.Cli.Tests.Mocks.SourceFixtures;
using CatalogueEntry = Jobranger.Cli.Catalogue.Catalogue;

namespace Jobranger.Cli.Tests.Features.Sources;

public sealed class FinnSourceTests {
    private const string recentKey = "finn_recent_since";
    private const string since = "2026-10-06T00:00:00+00:00";
    private const string applyUrl = "https://jobs.example.test/apply/1";
    private const string searchPath = "/job/search";
    private const string adPath = "/job/ad/";
    private const string finnHost = "www.finn.no";
    private const int categoryPageCap = 20;
    private const int recentPageCap = 50;

    private static readonly FinnSource source = new();

    private static readonly string adPage = SourceTestData("finn_ad_imported.html")
        + $"<script>\\\"applicationUrl\\\",\\\"{applyUrl}\\\"</script>";

    [Fact]
    public async Task ShouldStopCategoryScan_WhenLastPageIsReached() {
        FakeSiteHandler handler = Site([], [.. Enumerable.Range(1, 50).Select(id => SearchDoc(id))], categoryMatches: 500);

        await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Single(CategoryRequests(handler));
    }

    [Fact]
    public async Task ShouldStopCategoryScan_WhenPageReachesMatchCount() {
        FakeSiteHandler handler = Site([], [SearchDoc(2)], categoryMatches: 50, categoryLast: 5);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal((1, true), (CategoryRequests(handler).Count, batch.FinnSync!.Complete));
    }

    [Fact]
    public async Task ShouldMarkScanIncomplete_WhenCategoryPageCapIsReached() {
        FakeSiteHandler handler = Site([], [SearchDoc(2)], categoryMatches: 10_000, categoryLast: 999);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal((categoryPageCap, false), (CategoryRequests(handler).Count, batch.FinnSync!.Complete));
    }

    [Fact]
    public async Task ShouldSendOccupationCode_WhenScanningCategories() {
        FakeSiteHandler handler = Site([], [SearchDoc(2)]);

        await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal("occupation=0.23&page=1", CategoryRequests(handler)[0].Query.TrimStart('?'));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldFlagRepublished_WhenIdIsFarBelowNewest(bool recent) {
        JsonObject[] docs = [RecentDeveloperDoc(476500000), RecentDeveloperDoc(476400000), RecentDeveloperDoc(415573777)];
        FakeSiteHandler handler = recent ? Site(docs, [SearchDoc(9)]) : Site([], docs);

        SourceBatch batch = await source.FetchAsync(
            Context(handler, known: new HashSet<string> { "finn:9" }), CancellationToken.None);

        Assert.Equal(["415573777"], batch.Listings.Where(listing => listing.Republished).Select(listing => listing.SourceId));
    }

    [Fact]
    public async Task ShouldSkipKnownIds_WhenScanningCategories() {
        FakeSiteHandler handler = Site([], [SearchDoc(2), SearchDoc(3)]);

        SourceBatch batch = await source.FetchAsync(
            Context(handler, known: new HashSet<string> { "finn:2" }), CancellationToken.None);

        Assert.Equal(["finn:3"], batch.Listings.Select(listing => listing.Id));
    }

    [Fact]
    public async Task ShouldMatchJobTitle_WhenScanningRecentAds() {
        JsonObject[] docs = [
            SearchDoc(1, ("heading", "Er du klar for å bytte rede?"), ("job_title", "Data"), ("published", NewMs)),
            RecentDeveloperDoc(2, ("job_title", "Selger")),
            RecentDeveloperDoc(3),
        ];
        FakeSiteHandler handler = Site(docs, [SearchDoc(9)]);

        SourceBatch batch = await source.FetchAsync(
            Context(handler, known: new HashSet<string> { "finn:3", "finn:9" }), CancellationToken.None);

        Assert.Equal(["finn:1"], batch.Listings.Select(listing => listing.Id));
    }

    [Fact]
    public async Task ShouldDropSeniorTitle_WhenScanningRecentAds() {
        JsonObject[] docs = [RecentDeveloperDoc(1, ("heading", "Senior backend-utvikler")), RecentDeveloperDoc(2)];
        FakeSiteHandler handler = Site(docs, [SearchDoc(9)]);

        SourceBatch batch = await source.FetchAsync(
            Context(handler, known: new HashSet<string> { "finn:9" }), CancellationToken.None);

        Assert.Equal(["finn:2"], batch.Listings.Select(listing => listing.Id));
    }

    [Fact]
    public async Task ShouldStopRecentScan_WhenPageIsOlderThanCursor() {
        Dictionary<int, long> published = new() { [1] = NewMs, [2] = OldMs, [3] = OldMs };
        FakeSiteHandler handler = Site(
            page => [SearchDoc(page, ("published", published[page]))], [SearchDoc(9)], recentLast: 3);

        SourceBatch batch = await source.FetchAsync(Cursor(handler), CancellationToken.None);

        Assert.Equal((2, "2026-10-04T00:00:00+00:00"), (RecentRequests(handler).Count, batch.FinnSync!.WindowStart));
    }

    [Fact]
    public async Task ShouldStopRecentScan_WhenPageCapIsReached() {
        FakeSiteHandler handler = Site(page => [SearchDoc(page, ("published", NewMs))], [SearchDoc(9)], recentLast: 999);

        await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(recentPageCap, RecentRequests(handler).Count);
    }

    [Fact]
    public async Task ShouldLeaveWindowEmpty_WhenRecentPageHasNoAds() {
        FakeSiteHandler handler = Site([], [SearchDoc(9)]);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Null(batch.FinnSync!.WindowStart);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public async Task ShouldScanRecentAds_WhenCursorIsStoredOrNot(bool stored, int recentPages) {
        FakeSiteHandler handler = Site([SearchDoc(1, ("published", OldMs))], [SearchDoc(2)], recentLast: 2);

        await source.FetchAsync(stored ? Cursor(handler) : Context(handler), CancellationToken.None);

        Assert.Equal(recentPages, RecentRequests(handler).Count);
    }

    [Fact]
    public async Task ShouldStoreRecentCursorDayBeforeRun_WhenRecentScanSucceeds() {
        FakeSiteHandler handler = Site([], [SearchDoc(2)]);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal("2026-10-06T03:00:00+00:00", batch.Cursors[recentKey]);
    }

    [Fact]
    public async Task ShouldMarkRecentAdsUnlisted_WhenRecentScanFindsNewAd() {
        FakeSiteHandler handler = Site([RecentDeveloperDoc(7)], [SearchDoc(2)]);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(["finn:7"], batch.FinnSync!.UnlistedIds);
    }

    [Fact]
    public async Task ShouldKeepCategoryResults_WhenRecentScanFails() {
        FakeSiteHandler handler = Site([], [SearchDoc(2)], fail: "recent");

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.Equal(
            ("finn:2", 0),
            (Ids(error.Partial!.Listings), error.Partial.Cursors.Count));
    }

    [Fact]
    public async Task ShouldKeepCause_WhenRecentScanFails() {
        FakeSiteHandler handler = Site([], [SearchDoc(2)], fail: "recent");

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.IsType<HttpRequestException>(error.InnerException);
    }

    [Fact]
    public async Task ShouldFailWithIncompleteSync_WhenCategoryScanIsEmpty() {
        FakeSiteHandler handler = Site([RecentDeveloperDoc(7)], []);

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.Equal((false, "finn:7"), (error.Partial!.FinnSync!.Complete, Ids(error.Partial.Pages)));
    }

    [Fact]
    public async Task ShouldKeepRecentAds_WhenCategoryScanFails() {
        FakeSiteHandler handler = Site([RecentDeveloperDoc(7)], [], fail: "category");

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.Equal(
            ("finn:7", 1, "finn:7"),
            (Ids(error.Partial!.Listings), error.Partial.Read, Ids(error.Partial.Pages)));
    }

    [Fact]
    public async Task ShouldCountUniqueListings_WhenRecentAndCategoryOverlap() {
        FakeSiteHandler handler = Site([SearchDoc(7, ("published", NewMs))], [SearchDoc(7), SearchDoc(2)]);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(2, batch.Read);
    }

    [Fact]
    public async Task ShouldListAdOnce_WhenRecentAndCategoryFindIt() {
        FakeSiteHandler handler = Site([RecentDeveloperDoc(7)], [SearchDoc(7), SearchDoc(2)]);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal("finn:7,finn:2", Ids(batch.Listings));
    }

    [Fact]
    public async Task ShouldFillPage_WhenAdIsNew() {
        FakeSiteHandler handler = Site([], [SearchDoc(2)]);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        PageResult page = Assert.Single(batch.Pages);
        Assert.Equal(("finn:2", applyUrl, true), (page.ListingId, page.ApplyUrl, page.Description is not null));
    }

    [Fact]
    public async Task ShouldBackfillPage_WhenAdIsPending() {
        FakeSiteHandler handler = Site([], [SearchDoc(2)]);

        SourceBatch batch = await source.FetchAsync(
            Context(handler, pending: [FinnListing("9"), FinnListing("9") with { Id = "nav:u9", Source = "nav" }]),
            CancellationToken.None);

        Assert.Equal(["finn:9", "finn:2"], batch.Pages.Select(page => page.ListingId));
    }

    [Fact]
    public async Task ShouldLabelWithoutFetch_WhenTitleLevelIsSkipped() {
        FakeSiteHandler handler = Site([], [SearchDoc(2, ("heading", "Senior utvikler"))]);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(
            (new PageResult("finn:2", null, null, "senior"), 0),
            (Assert.Single(batch.Pages), AdRequests(handler).Count));
    }

    [Fact]
    public async Task ShouldFetchPage_WhenTitleLevelIsTargeted() {
        FakeSiteHandler handler = Site([], [SearchDoc(2, ("heading", "Senior utvikler"))]);
        Targets targets = new() { Limits = new Limits { Levels = ["junior", "senior"] } };
        SourcePlan plan = CatalogueEntry.Load().PlanFor(targets);

        SourceBatch batch = await source.FetchAsync(Context(handler, plan: plan), CancellationToken.None);

        Assert.Equal((1, true), (AdRequests(handler).Count, Assert.Single(batch.Pages).Description is not null));
    }

    [Theory]
    [InlineData("broken ld+json")]
    [InlineData("unparsable url")]
    [InlineData("url off finn")]
    [InlineData("server error")]
    [InlineData("lone surrogate description")]
    public async Task ShouldKeepFinnBatch_WhenOneAdPageIsBroken(string hostile) {
        JsonObject broken = hostile switch {
            "unparsable url" => SearchDoc(2, ("canonical_url", "http://[bad")),
            "url off finn" => SearchDoc(2, ("canonical_url", "https://jobs.example.test/job/ad/2")),
            _ => SearchDoc(2),
        };
        FakeSiteHandler handler = Site([], [broken, SearchDoc(3)], adAnswer: url => url.AbsolutePath.EndsWith("/2", StringComparison.Ordinal)
            ? HostileAd(hostile)
            : Body(adPage));

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(
            ("finn:2,finn:3", "finn:3", finnHost),
            (Ids(batch.Pages), Ids(batch.Pages.Where(page => page.Description is not null)),
                string.Join(',', handler.Urls.Select(url => url.Host).Distinct())));
    }

    [Theory]
    [InlineData("recent", "finn.recent.failed")]
    [InlineData("category", "finn.search.failed")]
    [InlineData("ad page", "finn.ad_text.failed")]
    public async Task ShouldLogFailure_WhenFinnStageFails(string stage, string eventName) {
        FakeSiteHandler handler = Site([], [SearchDoc(2)], fail: stage == "ad page" ? null : stage,
            adAnswer: stage == "ad page" ? _ => Status(HttpStatusCode.InternalServerError) : null);
        FakeLogger logger = new();

        await Record.ExceptionAsync(() => source.FetchAsync(Context(handler, logger: logger), CancellationToken.None));

        Assert.Equal(eventName, Events(logger));
    }

    [Fact]
    public async Task ShouldReturnFetchedPages_WhenCancelledDuringPages() {
        using CancellationTokenSource cancel = new();
        FakeSiteHandler handler = Site([], [SearchDoc(2), SearchDoc(3)]);
        handler.OnRequest = url => {
            if (url.AbsolutePath == adPath + "3") {
                cancel.Cancel();
            }
        };

        SourceBatch batch = await source.FetchAsync(Context(handler), cancel.Token);

        Assert.Equal(
            ("finn:2", 2),
            (Ids(batch.Pages), batch.Listings.Count));
    }

    private static FetchContext Cursor(FakeSiteHandler handler) =>
        Context(handler, cursors: new Dictionary<string, string> { [recentKey] = since });

    private static FakeSiteHandler Site(
        JsonObject[] recentDocs,
        JsonObject[] categoryDocs,
        string? fail = null,
        int recentLast = 1,
        int categoryMatches = -1,
        int categoryLast = 1,
        Func<Uri, HttpResponseMessage>? adAnswer = null) =>
        Site(_ => recentDocs, categoryDocs, fail, recentLast, categoryMatches, categoryLast, adAnswer);

    private static FakeSiteHandler Site(
        Func<int, JsonObject[]> recentDocs,
        JsonObject[] categoryDocs,
        string? fail = null,
        int recentLast = 1,
        int categoryMatches = -1,
        int categoryLast = 1,
        Func<Uri, HttpResponseMessage>? adAnswer = null) =>
        new(url => {
            if (url.AbsolutePath.StartsWith(adPath, StringComparison.Ordinal)) {
                return adAnswer?.Invoke(url) ?? Body(adPage);
            }
            bool recent = IsRecent(url);
            if (fail == (recent ? "recent" : "category")) {
                return Status(HttpStatusCode.NotFound);
            }
            if (recent) {
                JsonObject[] docs = recentDocs(int.Parse(HttpUtility.ParseQueryString(url.Query)["page"]!));
                return Body(SearchHtml(docs, docs.Length, recentLast));
            }
            return Body(SearchHtml(categoryDocs, categoryMatches < 0 ? categoryDocs.Length : categoryMatches, categoryLast));
        });

    private static bool IsRecent(Uri url) => HttpUtility.ParseQueryString(url.Query)["sort"] is not null;

    private static List<Uri> RecentRequests(FakeSiteHandler handler) =>
        [.. handler.Urls.Where(url => url.AbsolutePath == searchPath && IsRecent(url))];

    private static List<Uri> CategoryRequests(FakeSiteHandler handler) =>
        [.. handler.Urls.Where(url => url.AbsolutePath == searchPath && !IsRecent(url))];

    private static List<Uri> AdRequests(FakeSiteHandler handler) =>
        [.. handler.Urls.Where(url => url.AbsolutePath.StartsWith(adPath, StringComparison.Ordinal))];

    private static HttpResponseMessage HostileAd(string hostile) => hostile switch {
        "broken ld+json" => Body("<script type=\"application/ld+json\">{bad json</script>"),
        "lone surrogate description" => Body(
            """<script type="application/ld+json">{"script:ld+json": {"@type": "JobPosting", "description": "\ud800"}}</script>"""),
        "server error" => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(adPage) },
        _ => Body(adPage),
    };
}
