using System.Runtime.Versioning;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Tests.Mocks;
using static Jobranger.Cli.Tests.Mocks.AdFixtures;
using static Jobranger.Cli.Tests.Mocks.RunFixtures;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;

namespace Jobranger.Cli.Tests.Features.Runs;

[UnsupportedOSPlatform("windows")]
public sealed class AdsBatchWriterTests : IDisposable {
    private const string version = "v1";

    private readonly TestDatabase db = new();
    private readonly AdsEntry ads;
    private readonly AdsBatchWriter writer;

    public AdsBatchWriterTests() {
        ads = new AdsEntry(db.Connection, Clock());
        writer = new AdsBatchWriter(ads, version, Now);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldKeepTitleLevelLabel_WhenVersionIsRefreshed() {
        ads.Upsert(Pending(1, "Senior utvikler", "Org One AS"));
        writer.Write(Batch("finn") with { Pages = [new PageResult("finn:1", null, null, "senior")] });

        ads.RefreshAssessments(version);

        Assert.Equal(("senior", 0), (Column("finn:1", "label"), ads.Pending(3).Count));
    }

    [Fact]
    public void ShouldCountOnlyNewAds_WhenBatchRepeatsKnownAd() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));

        int created = writer.Write(Batch("finn", SourceFixtures.FinnListing("1"), SourceFixtures.FinnListing("2")));

        Assert.Equal(1, created);
    }

    [Fact]
    public void ShouldMarkApplySkipped_WhenPageHasNoApplyUrl() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS") with { Description = null });

        writer.Write(Batch("finn") with { Pages = [new PageResult("finn:1", Description, null)] });

        Assert.Equal("skipped|2026-10-09T10:00:00+00:00", Column("finn:1", "apply_status || '|' || apply_checked_at"));
    }

    [Fact]
    public void ShouldBumpDescriptionAttempts_WhenPageHasNoDescription() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS") with { Description = null });

        writer.Write(Batch("finn") with { Pages = [new PageResult("finn:1", null, null)] });

        Assert.Equal("1", Column("finn:1", "description_attempts"));
    }

    [Theory]
    [InlineData(true, "0")]
    [InlineData(false, "1")]
    public void ShouldCloseUnlistedFinnAd_WhenCategoryScanIsComplete(bool complete, string active) {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        db.Execute("UPDATE ads SET category_listed = 1");
        HashSet<string> none = [];

        writer.Write(Batch("finn") with { FinnSync = new FinnSync(none, none, null, [], complete) });

        Assert.Equal(active, Column("finn:1", "active"));
    }

    [Fact]
    public void ShouldCloseAd_WhenSourceStopsListingIt() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS") with { Id = "teamtailor:example:1", Source = "teamtailor:example" });

        writer.Write(Batch("teamtailor:example") with { ListedIds = new HashSet<string>() });

        Assert.Equal("0", Column("teamtailor:example:1", "active"));
    }

    [Fact]
    public void ShouldKeepAdOpen_WhenPartialBatchListsNothing() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS") with { Id = "teamtailor:example:1", Source = "teamtailor:example" });

        writer.Write(Batch("teamtailor:example") with { ListedIds = new HashSet<string>(), Partial = true });

        Assert.Equal("1", Column("teamtailor:example:1", "active"));
    }

    [Fact]
    public void ShouldKeepFinnAdOpen_WhenPartialBatchHasCompleteScan() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        db.Execute("UPDATE ads SET category_listed = 1");
        HashSet<string> none = [];

        writer.Write(Batch("finn") with { FinnSync = new FinnSync(none, none, null, [], true), Partial = true });

        Assert.Equal("1", Column("finn:1", "active"));
    }

    [Fact]
    public void ShouldSaveCursor_WhenBatchCarriesCursor() {
        writer.Write(Batch("nav") with { Cursors = new Dictionary<string, string> { ["nav_position"] = "42" } });

        Assert.Equal("42", ads.Cursors()["nav_position"]);
    }

    [Fact]
    public void ShouldReopenAd_WhenBatchMarksItActive() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        ads.SetActive("finn:1", active: false);

        writer.Write(Batch("finn") with { Active = new Dictionary<string, bool> { ["finn:1"] = true } });

        Assert.Equal("1", Column("finn:1", "active"));
    }

    [Fact]
    public void ShouldKeepJobTitles_WhenTwoBatchesCarryThem() {
        writer.Write(Batch("finn") with { JobTitles = new Dictionary<string, string> { ["finn:1"] = "Utvikler" } });
        writer.Write(Batch("finn") with { JobTitles = new Dictionary<string, string> { ["finn:2"] = "Tester" } });

        Assert.Equal(
            ["finn:1=Utvikler", "finn:2=Tester"],
            writer.JobTitles.Select(entry => $"{entry.Key}={entry.Value}").Order(StringComparer.Ordinal));
    }

    private string Column(string adId, string expression) =>
        Convert.ToString(db.Scalar($"SELECT {expression} FROM ads WHERE id = $0", adId), System.Globalization.CultureInfo.InvariantCulture)!;
}
