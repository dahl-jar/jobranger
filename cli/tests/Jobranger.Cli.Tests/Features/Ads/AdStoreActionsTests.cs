using System.Runtime.Versioning;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Applications;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Mocks;
using static Jobranger.Cli.Tests.Mocks.AdFixtures;
using ApplicationsEntry = Jobranger.Cli.Features.Applications.Applications;

namespace Jobranger.Cli.Tests.Features.Ads;

[UnsupportedOSPlatform("windows")]
public sealed class AdStoreActionsTests : IDisposable {
    private readonly TestDatabase db = new();
    private readonly AdStore store;
    private readonly ApplicationsEntry applications;

    public AdStoreActionsTests() {
        store = new(db.Connection, Clock());
        applications = new(db.Connection);
        store.Upsert(Primary);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldCreateApplication_WhenAdIsMarkedApplied() {
        store.MarkAd(Primary.Id, AdAction.Applied, Now, "cv.pdf");

        Application application = applications.List().Single();
        Assert.Equal(
            (Primary.Id, "Software Developer", "cv.pdf", "Applied"),
            (application.AdId, application.Role, application.CvName, application.Events.Single().Text));
    }

    [Fact]
    public void ShouldCopyAdDetails_WhenAppliedAdHasNoDisplayTitle() {
        db.Execute("UPDATE ads SET display_title = NULL, location = 'Oslo', deadline = '2026-11-01'");

        store.MarkAd(Primary.Id, AdAction.Applied, Now);

        Application application = applications.List().Single();
        Assert.Equal(("Utvikler", "Oslo", "2026-11-01", ""),
            (application.Role, application.Place, application.Deadline, application.Note));
    }

    [Fact]
    public void ShouldClearOtherDismissal_WhenMarkingAd() {
        store.MarkAd(Primary.Id, AdAction.NotForMe, Now);

        store.MarkAd(Primary.Id, AdAction.Skipped, Later);

        Assert.Equal([AdAction.Skipped], store.Actions()[Primary.Id]);
    }

    [Fact]
    public void ShouldKeepShowAnyway_WhenMarkingSkipped() {
        store.MarkAd(Primary.Id, AdAction.ShowAnyway, Now);

        store.MarkAd(Primary.Id, AdAction.Skipped, Later);

        Assert.Equal([AdAction.Skipped, AdAction.ShowAnyway], store.Actions()[Primary.Id].Order());
    }

    [Fact]
    public void ShouldListShownAnywayAd_WhenAdIsShownAnyway() {
        store.MarkAd(Primary.Id, AdAction.ShowAnyway, Now);

        Assert.Equal([Primary.Id], store.ShownAnyway());
    }

    [Fact]
    public void ShouldRemoveApplication_WhenAppliedIsUnmarked() {
        store.MarkAd(Primary.Id, AdAction.Applied, Now);

        store.UnmarkAd(Primary.Id, AdAction.Applied, Later);

        Application removed = applications.List(includeRemoved: true).Single();
        Assert.Equal((0, "2026-10-10T10:00:00+00:00", 1),
            (applications.List().Count, removed.RemovedAt, removed.Events.Count));
    }

    [Fact]
    public void ShouldRemoveApplication_WhenAppliedAdIsMarkedSkipped() {
        store.MarkAd(Primary.Id, AdAction.Applied, Now);

        store.MarkAd(Primary.Id, AdAction.Skipped, Later);

        Assert.Equal("2026-10-10T10:00:00+00:00", applications.List(includeRemoved: true).Single().RemovedAt);
    }

    [Fact]
    public void ShouldKeepApplication_WhenAdWithoutAppliedMarkIsSkipped() {
        applications.Create(new NewApplication("Org One AS", "Developer", "Bergen", null, "", Now, null, Primary.Id));

        store.MarkAd(Primary.Id, AdAction.Skipped, Later);

        Assert.Single(applications.List());
    }

    [Fact]
    public void ShouldRestoreApplication_WhenAdIsMarkedAppliedAgain() {
        store.MarkAd(Primary.Id, AdAction.Applied, Now);
        store.UnmarkAd(Primary.Id, AdAction.Applied, Now);

        store.MarkAd(Primary.Id, AdAction.Applied, Later);

        Assert.Equal([null], applications.List(includeRemoved: true).Select(application => application.RemovedAt));
    }

    [Fact]
    public void ShouldKeepApplication_WhenOtherActionIsUnmarked() {
        store.MarkAd(Primary.Id, AdAction.Applied, Now);
        store.MarkAd(Primary.Id, AdAction.ShowAnyway, Now);

        store.UnmarkAd(Primary.Id, AdAction.ShowAnyway, Later);

        Assert.Single(applications.List());
    }
}
