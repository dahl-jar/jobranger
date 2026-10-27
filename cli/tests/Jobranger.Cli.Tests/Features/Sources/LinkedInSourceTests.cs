using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Web;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Tests.Mocks;
using static Jobranger.Cli.Tests.Mocks.FakeSiteHandler;
using static Jobranger.Cli.Tests.Mocks.SourceFixtures;

namespace Jobranger.Cli.Tests.Features.Sources;

public sealed class LinkedInSourceTests {
    private const string sinceKey = "linkedin_since";
    private const string searchPath = "/search";
    private const string postingPath = "/jobPosting/";
    private const string emptyPage = "<!DOCTYPE html>\n\n<!---->  ";
    private const string signInPage = "<html><body>Sign in</body></html>";
    private const int pageCap = 10;

    private static readonly LinkedInSource source = new();
    private static readonly string searchHtml = SourceTestData("linkedin_search.html");
    private static readonly string postingHtml = SourceTestData("linkedin_posting.html");

    [Fact]
    public async Task ShouldMapListingFields_WhenPostingIsKept() {
        FakeSiteHandler handler = Site(FirstPageOnly, _ => Body(postingHtml));

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(["linkedin:4000000001", "linkedin:4000000003"], batch.Listings.Select(listing => listing.Id));
        SourceListing first = batch.Listings[0];
        Assert.Equal(new SourceListing {
            Id = "linkedin:4000000001",
            Source = "linkedin",
            SourceId = "4000000001",
            Title = "Software Engineer & Platform",
            Company = "Org One AS",
            Location = "Oslo, Oslo, Norway",
            Published = "2026-10-08",
            Url = "https://www.linkedin.com/jobs/view/4000000001",
            ApplyUrl = "https://www.linkedin.com/jobs/view/4000000001",
            Description = "About Org One: Build backend services in Java & Kotlin. "
                + "Bachelor's degree in computer science 0-2 years of experience",
        }, first);
        Assert.Equal((null, "Norway", 2), (batch.Listings[1].Published, batch.Listings[1].Location, batch.Read));
    }

    [Fact]
    public async Task ShouldSkipPostingFetch_WhenTitleIsNotDeveloper() {
        FakeSiteHandler handler = Site(FirstPageOnly, _ => Body(postingHtml));

        await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.DoesNotContain("4000000002", PostingIds(handler));
    }

    [Fact]
    public async Task ShouldSkipPostingFetch_WhenIdIsKnown() {
        FakeSiteHandler handler = Site(FirstPageOnly, _ => Body(postingHtml));
        HashSet<string> known = ["linkedin:4000000001"];

        SourceBatch batch = await source.FetchAsync(Context(handler, known: known), CancellationToken.None);

        Assert.Equal(("4000000003", "linkedin:4000000003"), (string.Join(',', PostingIds(handler)), Ids(batch.Listings)));
    }

    [Fact]
    public async Task ShouldSkipPostingFetch_WhenTitleIsSenior() {
        string page = searchHtml.Replace("Utvikler", "Senior utvikler", StringComparison.Ordinal);
        FakeSiteHandler handler = Site((_, start) => start == 0 ? page : emptyPage, _ => Body(postingHtml));

        await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.DoesNotContain("4000000003", PostingIds(handler));
    }

    [Theory]
    [InlineData("Internship")]
    [InlineData("Temporary")]
    public async Task ShouldKeepPosting_WhenEmploymentTypeIsKept(string employment) {
        string posting = postingHtml.Replace("Full-time", employment, StringComparison.Ordinal);
        FakeSiteHandler handler = Site(FirstPageOnly, _ => Body(posting));

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(2, batch.Listings.Count);
    }

    [Fact]
    public async Task ShouldDropPosting_WhenEmploymentIsNotKept() {
        string posting = postingHtml.Replace("Full-time", "Contract", StringComparison.Ordinal);
        FakeSiteHandler handler = Site(FirstPageOnly, _ => Body(posting));

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Empty(batch.Listings);
    }

    [Fact]
    public async Task ShouldKeepPosting_WhenEmploymentTypeIsMissing() {
        string posting = postingHtml.Replace("Employment type", "Job function", StringComparison.Ordinal);
        FakeSiteHandler handler = Site(FirstPageOnly, _ => Body(posting));

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(2, batch.Listings.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.OK)]
    public async Task ShouldSkipPosting_WhenPostingIsUnreadable(HttpStatusCode status) {
        FakeSiteHandler handler = Site(FirstPageOnly, id => id != "4000000001" ? Body(postingHtml)
            : status == HttpStatusCode.OK ? Body(signInPage) : Status(status));

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal("linkedin:4000000003", Ids(batch.Listings));
    }

    [Fact]
    public async Task ShouldFailWithoutCursor_WhenPostingIsRateLimited() {
        FakeSiteHandler handler = Site(FirstPageOnly,
            id => id == "4000000003" ? Status(HttpStatusCode.TooManyRequests) : Body(postingHtml));

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.Equal(
            ("linkedin:4000000001", 0, "linkedin search failed"),
            (Ids(error.Partial?.Listings), error.Partial?.Cursors.Count, error.Message));
    }

    [Fact]
    public async Task ShouldFail_WhenSearchPageHasNoCards() {
        FakeSiteHandler handler = Site((_, _) => "<html><body>Sign in to see more jobs</body></html>",
            _ => Body(postingHtml));

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.Equal(
            ("linkedin search failed", "", 0),
            (error.Message, Ids(error.Partial?.Listings), error.Partial?.Cursors.Count));
    }

    [Fact]
    public async Task ShouldSendSearchFilters_WhenSearchingEveryKeyword() {
        FakeSiteHandler handler = Site(FirstPageOnly, _ => Body(postingHtml));
        Dictionary<string, string> cursors = new() { [sinceKey] = "2026-10-05T03:00:00+00:00" };

        await source.FetchAsync(Context(handler, cursors: cursors), CancellationToken.None);

        List<NameValueCollection> searches = Searches(handler);
        Assert.Equal(("103819153", "r172800", "0"), (searches[0]["geoId"], searches[0]["f_TPR"], searches[0]["start"]));
        Assert.Equal(Plan.LinkedInKeywords.ToHashSet(), searches.Select(query => query["keywords"]!).ToHashSet());
    }

    [Fact]
    public async Task ShouldSearchLastWeek_WhenNoCursorIsStored() {
        FakeSiteHandler handler = Site((_, _) => emptyPage, _ => Body(postingHtml));

        await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal("r604800", Searches(handler)[0]["f_TPR"]);
    }

    [Fact]
    public async Task ShouldSearchLastWeek_WhenCursorIsUnreadable() {
        FakeSiteHandler handler = Site((_, _) => emptyPage, _ => Body(postingHtml));
        Dictionary<string, string> cursors = new() { [sinceKey] = "not a date" };

        await source.FetchAsync(Context(handler, cursors: cursors), CancellationToken.None);

        Assert.Equal("r604800", Searches(handler)[0]["f_TPR"]);
    }

    [Fact]
    public async Task ShouldStoreCursorDayBeforeRun_WhenFetchSucceeds() {
        FakeSiteHandler handler = Site((_, _) => emptyPage, _ => Body(postingHtml));

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal("2026-10-06T03:00:00+00:00", batch.Cursors[sinceKey]);
    }

    [Fact]
    public async Task ShouldStopKeyword_WhenPageIsEmpty() {
        FakeSiteHandler handler = Site((_, start) => start < 20 ? searchHtml : emptyPage, _ => Body(postingHtml));

        await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(["0", "10", "20"], FirstKeywordStarts(handler));
    }

    [Fact]
    public async Task ShouldStopKeyword_WhenPageCapIsReached() {
        FakeSiteHandler handler = Site((_, _) => searchHtml, _ => Body(postingHtml));

        await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(pageCap, FirstKeywordStarts(handler).Count);
    }

    [Fact]
    public async Task ShouldReturnListingsWithoutCursor_WhenCancelledDuringPostings() {
        using CancellationTokenSource cancel = new();
        FakeSiteHandler handler = Site(FirstPageOnly, _ => Body(postingHtml));
        handler.OnRequest = url => {
            if (url.AbsolutePath.EndsWith("4000000003", StringComparison.Ordinal)) {
                cancel.Cancel();
            }
        };

        SourceBatch batch = await source.FetchAsync(Context(handler), cancel.Token);

        Assert.Equal(("linkedin:4000000001", 0), (Ids(batch.Listings), batch.Cursors.Count));
    }

    private static string FirstPageOnly(string keyword, int start) => start == 0 ? searchHtml : emptyPage;

    private static FakeSiteHandler Site(Func<string, int, string> search, Func<string, HttpResponseMessage> posting) =>
        new(url => {
            if (url.AbsolutePath.EndsWith(searchPath, StringComparison.Ordinal)) {
                NameValueCollection query = HttpUtility.ParseQueryString(url.Query);
                return Body(search(query["keywords"]!, int.Parse(query["start"]!, CultureInfo.InvariantCulture)));
            }
            return posting(url.AbsolutePath[(url.AbsolutePath.LastIndexOf('/') + 1)..]);
        });

    private static List<string> PostingIds(FakeSiteHandler handler) =>
        [.. handler.Urls.Where(url => url.AbsolutePath.Contains(postingPath, StringComparison.Ordinal))
            .Select(url => url.AbsolutePath[(url.AbsolutePath.LastIndexOf('/') + 1)..])];

    private static List<NameValueCollection> Searches(FakeSiteHandler handler) =>
        [.. handler.Urls.Where(url => url.AbsolutePath.EndsWith(searchPath, StringComparison.Ordinal))
            .Select(url => HttpUtility.ParseQueryString(url.Query))];

    private static List<string> FirstKeywordStarts(FakeSiteHandler handler) =>
        [.. Searches(handler).Where(query => query["keywords"] == Plan.LinkedInKeywords[0]).Select(query => query["start"]!)];
}
