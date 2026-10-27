using System.Runtime.Versioning;
using Jobranger.Cli.Features.Applications;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Data.Sqlite;
using static Jobranger.Cli.Tests.Mocks.AdFixtures;
using ApplicationsEntry = Jobranger.Cli.Features.Applications.Applications;

namespace Jobranger.Cli.Tests.Features.Applications;

[UnsupportedOSPlatform("windows")]
public sealed class ApplicationsTests : IDisposable {
    private static readonly NewApplication manual =
        new("Org One AS", "Developer", "Bergen", "2026-11-01", "", Now, "cv.pdf", AdId: null);

    private readonly TestDatabase db = new();
    private readonly ApplicationsEntry applications;

    public ApplicationsTests() {
        applications = new(db.Connection);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldReadBackFields_WhenNoteChanged() {
        long id = applications.Create(manual);

        applications.Update(id, new ApplicationChange(Note: "Called back"), Later);

        Application read = applications.List().Single();
        Assert.Equal(
            (id, null, "Org One AS", "Developer", "Bergen", "2026-11-01", ApplicationStatus.Waiting, "Called back",
                "2026-10-09T10:00:00+00:00", "cv.pdf", null, "Applied"),
            (read.Id, read.AdId, read.Company, read.Role, read.Place, read.Deadline, read.Status, read.Note,
                read.AppliedAt, read.CvName, read.RemovedAt, read.Events.Single().Text));
    }

    [Fact]
    public void ShouldAppendEvent_WhenEventIsAdded() {
        long id = applications.Create(manual);

        applications.AddEvent(id, Later, "Phone screen");

        Application read = applications.List().Single();
        Assert.Equal(
            [new ApplicationEvent("2026-10-09T10:00:00+00:00", "Applied"),
                new ApplicationEvent("2026-10-10T10:00:00+00:00", "Phone screen")],
            read.Events);
    }

    [Fact]
    public void ShouldAppendStatusEvent_WhenStatusChanges() {
        long id = applications.Create(manual);

        applications.Update(id, new ApplicationChange(Status: ApplicationStatus.Offer), Later);

        Application read = applications.List().Single();
        Assert.Equal((ApplicationStatus.Offer, new ApplicationEvent("2026-10-10T10:00:00+00:00", "Status: Offer")),
            (read.Status, read.Events[^1]));
    }

    [Fact]
    public void ShouldRestoreApplicationWithEvents_WhenRemovedOneIsRestored() {
        long id = applications.Create(manual);
        applications.Update(id, new ApplicationChange(Status: ApplicationStatus.Interview), Now);
        applications.Remove(id, Later);

        applications.Restore(id);

        Application restored = applications.List().Single();
        Assert.Equal(["Applied", "Status: Interview"], restored.Events.Select(item => item.Text));
    }

    [Fact]
    public void ShouldHideApplication_WhenRemoved() {
        long id = applications.Create(manual);
        applications.Remove(id, Later);

        IReadOnlyList<Application> listed = applications.List();

        Assert.Empty(listed);
    }

    [Fact]
    public void ShouldListRemovedApplication_WhenRemovedAreIncluded() {
        long id = applications.Create(manual);
        applications.Remove(id, Later);

        IReadOnlyList<Application> listed = applications.List(includeRemoved: true);

        Assert.Equal([id], listed.Select(application => application.Id));
    }

    [Fact]
    public void ShouldListNewestApplicationFirst_WhenListing() {
        applications.Create(manual);
        applications.Create(manual with { Company = "Org Two AS", AppliedAt = Later });

        Assert.Equal(["Org Two AS", "Org One AS"], applications.List().Select(item => item.Company));
    }

    [Fact]
    public void ShouldReadNoEvents_WhenApplicationHasNone() {
        applications.Create(manual);
        db.Execute("DELETE FROM application_events");

        Application read = applications.List().Single();

        Assert.Empty(read.Events);
    }

    [Fact]
    public void ShouldRejectApplication_WhenCompanyIsBlank() {
        Assert.Throws<SqliteException>(() => applications.Create(manual with { Company = " " }));
    }
}
