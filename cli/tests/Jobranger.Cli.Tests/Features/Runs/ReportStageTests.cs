using System.Net;
using System.Runtime.Versioning;
using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using static Jobranger.Cli.Tests.Mocks.AdFixtures;
using static Jobranger.Cli.Tests.Mocks.RunFixtures;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Features.Runs;

[UnsupportedOSPlatform("windows")]
public sealed class ReportStageTests : IDisposable {
    private const string version = MatchingFixtures.Version;
    private const string applyUrl = "https://apply.example.test/jobs/1";
    private const string finnAdId = "finn:1";
    private const string finnHeading = ".NET-utvikler – moderne samfunnsnyttige løsninger";
    private const string jobTitle = ".NET-utvikler";
    private const string statusQuery = "SELECT apply_status, closed_reason FROM ads WHERE id = 'finn:1'";

    private static readonly EmployerFeed feed = new("teamtailor", "computas", "Computas", "https://careers.example.test/jobs.rss");
    private static readonly IReadOnlyDictionary<string, string> jobTitles =
        new Dictionary<string, string> { [finnAdId] = jobTitle };

    private readonly TestDatabase db = new();
    private readonly AdsEntry ads;
    private readonly AdStore store;

    public ReportStageTests() {
        ads = new AdsEntry(db.Connection, Clock());
        store = new AdStore(db.Connection, Clock());
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task ShouldKeepFinnAd_WhenApplyLinkIsActive() {
        Classified(Pending(1, finnHeading, "Computas AS") with { ApplyUrl = applyUrl });
        FeedAd("Kotlin-utvikler");

        await RunAsync(Input([feed]), new FakeSiteHandler(_ => FakeSiteHandler.Body("<html>Søk nå</html>")));

        Assert.Equal(["active|NULL"], TestDatabase.Rows(db.Connection, statusQuery));
    }

    [Fact]
    public async Task ShouldKeepFinnAd_WhenFeedListsJobTitle() {
        Classified(Pending(1, finnHeading, "Computas AS"));
        FeedAd(jobTitle);

        await RunAsync(Input([feed]));

        Assert.Equal(["NULL|NULL"], TestDatabase.Rows(db.Connection, statusQuery));
    }

    [Fact]
    public async Task ShouldExpireFinnAd_WhenFeedMissesJobTitle() {
        Classified(Pending(1, finnHeading, "Computas AS"));
        FeedAd("Kotlin-utvikler");

        await RunAsync(Input([feed]));

        Assert.Equal(["expired|filled"], TestDatabase.Rows(db.Connection, statusQuery));
    }

    [Fact]
    public async Task ShouldCarryAd_WhenMatchedBeforeRunStarted() {
        Classified(Pending(1, "Utvikler", "Org One AS"));
        MatchedAt(Now.AddDays(-2));

        ReportOutcome outcome = await RunAsync(Input([]));

        Assert.Equal((finnAdId, finnAdId), (Ids(outcome.ReportedIds), Ids(outcome.CarriedIds)));
    }

    [Fact]
    public async Task ShouldSilenceAd_WhenMatchedBeforeCarryWindow() {
        Classified(Pending(1, "Utvikler", "Org One AS"));
        MatchedAt(Now.AddDays(-8));

        ReportOutcome outcome = await RunAsync(Input([]));

        Assert.Equal(("", finnAdId), (Ids(outcome.ReportedIds), Ids(outcome.SilentIds)));
    }

    [Fact]
    public async Task ShouldReportAd_WhenShownAnywayAfterCarryWindow() {
        Classified(Pending(1, "Utvikler", "Org One AS"));
        MatchedAt(Now.AddDays(-8));
        store.MarkAd(finnAdId, AdAction.ShowAnyway, Now);

        ReportOutcome outcome = await RunAsync(Input([]));

        Assert.Equal([finnAdId], outcome.ReportedIds);
    }

    [Fact]
    public async Task ShouldSilenceAd_WhenPublishedOverNinetyDaysAgo() {
        Classified(Pending(1, "Utvikler", "Org One AS") with {
            Published = StoredTime.Format(Today.AddDays(-91)) + "T00:00:00+00:00",
        });

        ReportOutcome outcome = await RunAsync(Input([]));

        Assert.Equal(("", finnAdId), (Ids(outcome.ReportedIds), Ids(outcome.SilentIds)));
    }

    [Theory]
    [InlineData(30, false)]
    [InlineData(100, true)]
    public async Task ShouldSilenceAd_WhenSameVacancyWasReported(int daysAgo, bool reported) {
        Classified(Pending(1, "Utvikler", "Org One AS"));
        Classified(Pending(2, "Utvikler", "Org One AS") with { Id = "nav:2", Source = "nav" });
        store.MarkReported(["nav:2"], Now.AddDays(-daysAgo));

        ReportOutcome outcome = await RunAsync(Input([]));

        Assert.Equal(reported, outcome.ReportedIds.Contains(finnAdId));
    }

    [Fact]
    public async Task ShouldCountHiddenKinds_WhenNewAdsAreHidden() {
        Classified(Pending(1, "Senior utvikler", "Org One AS"), "senior");
        ads.Upsert(Pending(2, "Selger", "Org Two AS"));
        db.Execute("UPDATE ads SET is_programming_job = 0 WHERE id = 'finn:2'");

        ReportOutcome outcome = await RunAsync(Input([]));

        Assert.Equal(new Dictionary<HiddenKind, int> { [HiddenKind.Senior] = 1 }, outcome.HiddenCounts);
    }

    private static string Ids(IReadOnlyList<string> ids) => string.Join(',', ids);

    private static ReportInput Input(IReadOnlyList<EmployerFeed> readFeeds) =>
        new(MatchingFixtures.AllRoles(), version, Today, Now, readFeeds, jobTitles);

    private void Classified(Ad ad, string label = "junior") {
        ads.Upsert(ad);
        ads.SetClassification(ad.Id, AdFixtures.Classification with {
            Label = label,
            Assessment = MatchingEntry.EncodeMatch(MatchingFixtures.StrongMatch, []),
        }, version);
    }

    private void FeedAd(string title) => ads.Upsert(Pending(9, title, "Computas") with {
        Id = "teamtailor:computas:9",
        Source = "teamtailor:computas",
        Location = "Oslo",
    });

    private void MatchedAt(DateTimeOffset at) =>
        db.Execute("UPDATE ads SET first_seen = $0, first_classified_at = $0", StoredTime.Format(at));

    private async Task<ReportOutcome> RunAsync(ReportInput input, FakeSiteHandler? site = null) {
        using HttpClient http = new(site ?? new FakeSiteHandler(_ => FakeSiteHandler.Status(HttpStatusCode.NotFound)));
        ReportStage stage = new(ads, http, Clock(), new RecordingRunEvents(), NullLogger.Instance);
        return await stage.RunAsync(input, CancellationToken.None);
    }
}
