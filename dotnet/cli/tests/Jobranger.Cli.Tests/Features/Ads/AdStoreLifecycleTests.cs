using System.Runtime.Versioning;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;
using static Jobranger.Cli.Tests.Mocks.AdFixtures;

namespace Jobranger.Cli.Tests.Features.Ads;

[UnsupportedOSPlatform("windows")]
public sealed class AdStoreLifecycleTests : IDisposable {
    private static readonly DateTimeOffset windowStart = new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] none = [];

    private readonly TestDatabase db = new();
    private readonly FakeTimeProvider clock = Clock();
    private readonly AdStore store;

    public AdStoreLifecycleTests() {
        store = new(db.Connection, clock);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldReopenListedAd_WhenSourceSyncs() {
        store.Upsert(Numbered(1));
        store.SetActive("finn:1", active: false);

        store.SyncSourceActive("finn", ["finn:1"]);

        Assert.Equal(["finn:1"], ActiveIds());
    }

    [Fact]
    public void ShouldReopenAd_WhenSetActiveAgain() {
        store.Upsert(Primary);
        store.SetActive(Primary.Id, active: false);

        store.SetActive(Primary.Id, active: true);

        Assert.Equal([Primary.Id], ActiveIds());
    }

    [Fact]
    public void ShouldKeepWideAdActive_WhenPublishedBeforeWindow() {
        store.Upsert(Primary with { Published = "2026-09-20T00:00:00+00:00" });
        store.MarkCategoryUnlisted([Primary.Id]);

        store.SyncFinnActive(none, none, windowStart);

        Assert.Equal([Primary.Id], ActiveIds());
    }

    [Fact]
    public void ShouldCloseWideAd_WhenMissingInsideWindow() {
        store.Upsert(Primary with { Published = "2026-10-02T00:00:00+00:00" });
        store.Upsert(Primary with { Id = "nav:1", Source = "nav", SourceId = "1" });
        store.MarkCategoryUnlisted([Primary.Id]);

        store.SyncFinnActive(none, none, windowStart);

        Assert.Equal(["nav:1"], ActiveIds());
    }

    [Fact]
    public void ShouldKeepWideAdActive_WhenRecentScanListsIt() {
        store.Upsert(Primary with { Published = "2026-10-02T00:00:00+00:00" });
        store.MarkCategoryUnlisted([Primary.Id]);

        store.SyncFinnActive(none, [Primary.Id], windowStart);

        Assert.Equal([Primary.Id], ActiveIds());
    }

    [Fact]
    public void ShouldCloseCategoryAd_WhenCategoryScanMissesIt() {
        store.Upsert(Primary);

        store.SyncFinnActive(none, none, windowStart: null);

        Assert.Empty(ActiveIds());
    }

    [Fact]
    public void ShouldCloseRecentAd_WhenCategoryScanDropsItLater() {
        store.Upsert(Primary with { Published = "2026-09-20T00:00:00+00:00" });
        store.MarkCategoryUnlisted([Primary.Id]);
        store.SyncFinnActive([Primary.Id], none, windowStart);

        store.SyncFinnActive(none, none, windowStart);

        Assert.Empty(ActiveIds());
    }

    [Fact]
    public void ShouldReopenClosedFinnAd_WhenListedAgain() {
        store.Upsert(Primary);
        store.SyncFinnActive(none, none, windowStart: null);

        store.SyncFinnActive([Primary.Id], none, windowStart: null);

        Assert.Equal([Primary.Id], ActiveIds());
    }

    [Fact]
    public void ShouldCloseAsRemoved_WhenAdLeavesSource() {
        store.Upsert(Primary);

        store.SyncSourceActive("finn", none);

        ClosedAd closed = store.Closed(Today).Single();
        Assert.Equal((Primary.Id, ClosedReason.Removed, "2026-10-09"), (closed.Ad.Id, closed.Reason, closed.ClosedOn));
    }

    [Fact]
    public void ShouldCloseAsFilled_WhenApplyLinkExpires() {
        store.Upsert(Primary);

        store.SetApplyStatus(Primary.Id, "expired", Now);

        ClosedAd closed = store.Closed(Today).Single();
        Assert.Equal((ClosedReason.Filled, "2026-10-09"), (closed.Reason, closed.ClosedOn));
    }

    [Fact]
    public void ShouldKeepAdOpen_WhenApplyLinkIsActive() {
        store.Upsert(Primary);

        store.SetApplyStatus(Primary.Id, "active", Now);

        Assert.Empty(store.Closed(Today));
    }

    [Fact]
    public void ShouldClearClosedTime_WhenAdReturns() {
        store.Upsert(Primary);
        store.SyncSourceActive("finn", none);

        store.SyncSourceActive("finn", [Primary.Id]);

        IReadOnlyList<ClosedAd> closed = store.Closed(Today);
        Ad reopened = store.Active().Single();
        Assert.Equal((0, null), (closed.Count, reopened.ClosedAt));
    }

    [Fact]
    public void ShouldListClosedAdsWithinThirtyDays_WhenListingClosed() {
        store.Upsert(Numbered(2) with { Deadline = "2026-08-01" });
        store.ClosePastDeadline(new DateOnly(2026, 8, 31));
        store.Upsert(Numbered(1) with { Deadline = "2026-08-01" });
        store.ClosePastDeadline(new DateOnly(2026, 9, 1));
        store.MarkAd("finn:1", AdAction.Applied, Now);

        IReadOnlyList<ClosedAd> closed = store.Closed(new DateOnly(2026, 10, 1));

        Assert.Equal([("finn:1", ClosedReason.Deadline, (AdAction?)AdAction.Applied)],
            closed.Select(item => (item.Ad.Id, item.Reason, item.Action)));
    }

    [Fact]
    public void ShouldCountAdsClosedSinceTime_WhenCountingClosed() {
        store.Upsert(Numbered(1) with { Deadline = "2026-08-01" });
        store.ClosePastDeadline(new DateOnly(2026, 9, 1));
        store.Upsert(Numbered(2) with { Deadline = "2026-09-15" });
        store.Upsert(Numbered(3) with { Deadline = "2026-09-01" });
        store.ClosePastDeadline(new DateOnly(2026, 9, 15));
        store.ClosePastDeadline(new DateOnly(2026, 10, 1));

        long count = store.ClosedSince("2026-09-15");

        Assert.Equal(2, count);
    }

    private List<string> ActiveIds() => [.. store.Active().Select(ad => ad.Id)];
}
