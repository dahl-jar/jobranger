using System.Runtime.Versioning;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;
using static Jobranger.Cli.Tests.Mocks.AdFixtures;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;

namespace Jobranger.Cli.Tests.Features.Ads;

[UnsupportedOSPlatform("windows")]
public sealed class AdsTests : IDisposable {
    private const int maxAttempts = 3;

    private static readonly Func<Ad, bool> anyAd = _ => true;

    private readonly TestDatabase db = new();
    private readonly FakeTimeProvider clock = Clock();
    private readonly AdsEntry ads;
    private readonly AdStore store;

    public AdsTests() {
        ads = new(db.Connection, clock);
        store = new(db.Connection, clock);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldStoreAdOnce_WhenUpsertedTwice() {
        bool first = ads.Upsert(Primary);

        bool second = ads.Upsert(Primary);

        Assert.Equal([true, false], [first, second]);
    }

    [Fact]
    public void ShouldLeaveOutAd_WhenLabelAttemptsRunOut() {
        Ad fresh = Numbered(1) with { Description = "krav: 5 års erfaring", Assessment = null };
        Ad exhausted = Numbered(2) with { Description = "ingen krav", Assessment = null };
        ads.Upsert(fresh);
        ads.Upsert(exhausted);
        store.BumpLabelAttempts([exhausted.Id, exhausted.Id, exhausted.Id]);

        IReadOnlyList<Ad> pending = ads.Pending(maxAttempts);

        Assert.Equal([fresh.Id], pending.Select(ad => ad.Id));
    }

    [Fact]
    public void ShouldCountOnlyActiveAds_WhenCountingOutdated() {
        ads.Upsert(Numbered(1) with { AssessmentVersion = OutdatedVersion });
        ads.Upsert(Numbered(2) with { Assessment = null, AssessmentVersion = null });
        ads.Upsert(Numbered(3));
        ads.Upsert(Numbered(4) with { AssessmentVersion = OutdatedVersion });
        store.SetActive("finn:4", active: false);

        Assert.Equal(2, ads.CountOutdated(CurrentVersion));
    }

    [Fact]
    public void ShouldReadBackClassification_WhenAdIsClassified() {
        ads.Upsert(Primary with { Assessment = null, AssessmentVersion = null });

        ads.SetClassification(Primary.Id, Classification, CurrentVersion);

        Ad stored = store.ById([Primary.Id]).Single();
        Assert.Equal(
            ("junior", "Build Java services.", false, true, "Backend Developer", "backend", "Java|Søk",
                "Bachelor's degree", Assessment, CurrentVersion),
            (stored.Label, stored.Summary, stored.RequiresMaster, stored.IsProgrammingJob, stored.DisplayTitle,
                stored.RoleType, string.Join('|', stored.Technologies), string.Join('|', stored.Requirements),
                stored.Assessment, stored.AssessmentVersion));
    }

    [Fact]
    public void ShouldStoreListsAsPythonJson_WhenAdIsClassified() {
        ads.Upsert(Primary);

        ads.SetClassification(Primary.Id, Classification, CurrentVersion);

        Assert.Equal("[\"Java\", \"Søk\"]", db.Scalar("SELECT technologies FROM ads"));
    }

    [Fact]
    public void ShouldKeepFirstClassificationTime_WhenReclassified() {
        ads.Upsert(Primary with { Assessment = null });
        ads.SetClassification(Primary.Id, Classification, CurrentVersion);
        clock.Advance(TimeSpan.FromDays(1));

        ads.SetClassification(Primary.Id, Classification, OutdatedVersion);

        Ad stored = store.ById([Primary.Id]).Single();
        Assert.Equal("2026-10-09T10:00:00+00:00", stored.FirstClassifiedAt);
    }

    [Fact]
    public void ShouldKeepLabelCurrent_WhenLabelIsSetWithVersion() {
        ads.Upsert(Primary with { AssessmentVersion = OutdatedVersion });

        ads.SetLabel(Primary.Id, "senior", CurrentVersion);

        Assert.Equal(0, ads.CountOutdated(CurrentVersion));
    }

    [Fact]
    public void ShouldListOnlyActiveAdsBeforeDeadline_WhenListingReportable() {
        ads.Upsert(Numbered(1) with { Deadline = "2026-10-09" });
        ads.Upsert(Numbered(2));
        ads.Upsert(Numbered(3) with { Deadline = "2026-10-08" });
        store.SetActive("finn:2", active: false);

        IReadOnlyList<Ad> reportable = ads.Reportable(Today, anyAd);

        Assert.Equal(["finn:1"], reportable.Select(ad => ad.Id));
    }

    [Fact]
    public void ShouldLeaveOutAd_WhenAlreadyReported() {
        ads.Upsert(Primary);

        ads.MarkReported([Primary.Id], Now);

        Assert.Empty(ads.Reportable(Today, anyAd));
    }

    [Fact]
    public void ShouldLeaveOutAd_WhenApplyLinkExpired() {
        ads.Upsert(Primary with { ApplyUrl = "https://apply.example.test/1" });

        store.SetApplyStatus(Primary.Id, "expired", Now);

        Assert.Empty(ads.Reportable(Today, anyAd));
    }

    [Fact]
    public void ShouldLeaveOutAd_WhenRuleRejectsIt() {
        ads.Upsert(Numbered(1));
        ads.Upsert(Numbered(2) with { RequiresMaster = true });

        IReadOnlyList<Ad> reportable = ads.Reportable(Today, ad => !ad.RequiresMaster);

        Assert.Equal(["finn:1"], reportable.Select(ad => ad.Id));
    }

    [Fact]
    public void ShouldKeepShownAnywayAd_WhenRuleRejectsIt() {
        ads.Upsert(Primary with { RequiresMaster = true });
        store.MarkAd(Primary.Id, AdAction.ShowAnyway, Now);

        IReadOnlyList<Ad> reportable = ads.Reportable(Today, ad => !ad.RequiresMaster);

        Assert.Equal([Primary.Id], reportable.Select(ad => ad.Id));
    }

    [Theory]
    [InlineData(nameof(AdAction.Applied))]
    [InlineData(nameof(AdAction.Skipped))]
    [InlineData(nameof(AdAction.NotForMe))]
    public void ShouldLeaveOutDismissedAd_WhenRuleAcceptsIt(string action) {
        ads.Upsert(Primary);
        store.MarkAd(Primary.Id, Enum.Parse<AdAction>(action), Now);

        Assert.Empty(ads.Reportable(Today, anyAd));
    }

    [Fact]
    public void ShouldDropDismissedAd_WhenKeepingReportable() {
        ads.Upsert(Numbered(1));
        ads.Upsert(Numbered(2));
        store.MarkAd("finn:2", AdAction.Skipped, Now);

        IReadOnlyList<Ad> kept = ads.KeepReportable(store.Active(), anyAd);

        Assert.Equal(["finn:1"], kept.Select(ad => ad.Id));
    }
}
