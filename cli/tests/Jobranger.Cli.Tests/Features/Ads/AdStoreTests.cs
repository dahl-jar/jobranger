using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using static Jobranger.Cli.Tests.Mocks.AdFixtures;

namespace Jobranger.Cli.Tests.Features.Ads;

[UnsupportedOSPlatform("windows")]
public sealed class AdStoreTests : IDisposable {
    private const int maxAttempts = 3;
    private const int limit = 10;
    private const string insertedColumnsQuery =
        "SELECT id, source, source_id, title, company, location, published, deadline, url, description, label, "
        + "summary, requires_master, is_programming_job, display_title, role_type, technologies, requirements, "
        + "assessment, apply_url, apply_status, apply_checked_at, republished, assessment_version, dedup_key "
        + "FROM ads WHERE id = 'finn:1'";

    private static readonly string[] skippedLevels = ["senior", "lead"];
    private static readonly DateTimeOffset weekAgo = Now.AddDays(-7);

    private readonly TestDatabase db = new();
    private readonly FakeTimeProvider clock = Clock();
    private readonly AdStore store;

    public AdStoreTests() {
        store = new(db.Connection, clock);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldWriteSameColumns_WhenUpsertingAdReadFromPythonDatabase() {
        using SqliteConnection python = TestDatabase.OpenRaw(TestDatabase.PythonDatabase());
        Ad golden = new AdStore(python, clock).ById(["finn:1"]).Single();

        store.Upsert(golden);

        using SqliteConnection mine = TestDatabase.OpenRaw(db.Path);
        Assert.Equal(TestDatabase.Rows(python, insertedColumnsQuery), TestDatabase.Rows(mine, insertedColumnsQuery));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("error")]
    public void ShouldGiveFreshAttempts_WhenUnassessedAdIsOutdated(string? label) {
        store.Upsert(Primary with { Label = label, Description = "Build Java services.", AssessmentVersion = OutdatedVersion });
        store.BumpLabelAttempts([Primary.Id, Primary.Id, Primary.Id]);

        store.ResetUnassessed(CurrentVersion);

        Ad pending = store.PendingClassification(maxAttempts).Single();
        string? stored = (string?)db.Scalar("SELECT label_attempts || '|' || assessment_version FROM ads");
        Assert.Equal((Primary.Id, "0|" + CurrentVersion), (pending.Id, stored));
    }

    [Fact]
    public void ShouldResetLabelAttempts_WhenClassificationIsStored() {
        store.Upsert(Primary with { Description = "Build Java services." });
        store.BumpLabelAttempts([Primary.Id, Primary.Id]);

        store.SetClassification(Primary.Id, Classification, CurrentVersion);

        Assert.Equal(0L, db.Scalar("SELECT label_attempts FROM ads"));
    }

    [Fact]
    public void ShouldListExhaustedAd_WhenLabelAttemptsRunOut() {
        store.Upsert(Numbered(1) with { Description = "x" });
        store.Upsert(Numbered(2) with { Description = "x" });
        store.BumpLabelAttempts(["finn:2", "finn:2", "finn:2"]);

        Assert.Equal(["finn:2"], store.ExhaustedClassification(maxAttempts, CurrentVersion).Select(ad => ad.Id));
    }

    [Fact]
    public void ShouldSkipDescription_WhenLabelIsSkippedLevel() {
        store.Upsert(Primary);
        store.Upsert(Numbered(2));
        store.SetLabel(Primary.Id, "senior", CurrentVersion);

        IReadOnlyList<Ad> pending = store.PendingDescription(skippedLevels);

        Assert.Equal(["finn:2"], pending.Select(ad => ad.Id));
    }

    [Fact]
    public void ShouldStopListingDescription_WhenThreeFetchesFailed() {
        store.Upsert(Primary);
        store.Upsert(Numbered(2));
        for (int attempt = 0; attempt < maxAttempts; attempt++) {
            store.BumpDescriptionAttempts(Primary.Id);
        }
        store.BumpDescriptionAttempts("finn:2");

        IReadOnlyList<Ad> pending = store.PendingDescription(skippedLevels);

        Assert.Equal(["finn:2"], pending.Select(ad => ad.Id));
    }

    [Fact]
    public void ShouldKeepApplyUrl_WhenDescriptionComesWithout() {
        store.Upsert(Primary with { ApplyUrl = "https://apply.example.test/1" });

        store.SetDescription(Primary.Id, "Build services.", applyUrl: null);

        Ad stored = store.ById([Primary.Id]).Single();
        Assert.Equal(("Build services.", "https://apply.example.test/1"), (stored.Description, stored.ApplyUrl));
    }

    [Fact]
    public void ShouldListOnlyFinnAdWithTextAndNoApplyUrl_WhenListingPendingApplyUrl() {
        store.Upsert(Numbered(1) with { Label = "junior", Description = "x" });
        store.Upsert(Numbered(2) with { Label = "junior", Description = "x", ApplyUrl = "https://apply.example.test/2" });
        store.Upsert(Numbered(3) with { Label = "junior" });
        store.Upsert(Numbered(4) with { Label = "senior", Description = "x" });
        store.Upsert(Primary with { Id = "nav:5", Source = "nav", SourceId = "5", Label = "junior", Description = "x" });

        IReadOnlyList<Ad> pending = store.PendingApplyUrl("finn", limit, AllTargets);

        Assert.Equal(["finn:1"], pending.Select(ad => ad.Id));
    }

    [Fact]
    public void ShouldListNewestFirst_WhenListingPendingApplyUrl() {
        store.Upsert(Numbered(1) with { Description = "x" });
        clock.Advance(TimeSpan.FromSeconds(1));
        store.Upsert(Numbered(2) with { Description = "x" });

        IReadOnlyList<Ad> pending = store.PendingApplyUrl("finn", 1, AllTargets);

        Assert.Equal(["finn:2"], pending.Select(ad => ad.Id));
    }

    [Fact]
    public void ShouldLeaveOutMasterAd_WhenMastersAreHidden() {
        store.Upsert(Numbered(1) with { Description = "x", RequiresMaster = true });

        Assert.Empty(store.PendingApplyUrl("finn", limit, AllTargets));
    }

    [Fact]
    public void ShouldListMasterAd_WhenMastersAreShown() {
        store.Upsert(Numbered(1) with { Description = "x", RequiresMaster = true });
        Targets shown = AllTargets with { Limits = new Limits { HideMasters = false } };

        Assert.Single(store.PendingApplyUrl("finn", limit, shown));
    }

    [Fact]
    public void ShouldListAdOfEveryLevel_WhenAllLevelsAreTargeted() {
        store.Upsert(Numbered(1) with { Label = "lead", Description = "x" });
        store.Upsert(Numbered(2) with { Label = "none", Description = "x" });
        store.Upsert(Numbered(3) with { Label = "error", Description = "x" });
        Targets everyLevel = AllTargets with { Limits = new Limits { Levels = Limits.AllLevels } };

        IReadOnlyList<Ad> pending = store.PendingApplyUrl("finn", limit, everyLevel);

        Assert.Equal(["finn:1", "finn:2", "finn:3"], pending.Select(ad => ad.Id).Order());
    }

    [Fact]
    public void ShouldLeaveOutAd_WhenRoleIsNotTargeted() {
        store.Upsert(Numbered(1) with { Description = "x", RoleType = "frontend" });
        Targets backendOnly = AllTargets with { Subfields = ["backend"] };

        Assert.Empty(store.PendingApplyUrl("finn", limit, backendOnly));
    }

    [Fact]
    public void ShouldListApplyChecksOncePerWeek_WhenLinksWereChecked() {
        foreach (int index in new[] { 1, 2, 3 }) {
            store.Upsert(Numbered(index) with { Label = "junior", ApplyUrl = "https://apply.example.test/" + index });
        }
        store.Upsert(Numbered(4) with { Label = "senior", ApplyUrl = "https://apply.example.test/4" });
        store.Upsert(Numbered(5) with { Label = "junior" });
        store.SetApplyStatus("finn:2", "active", Now.AddDays(-2));
        store.SetApplyStatus("finn:3", "active", Now.AddDays(-15));

        IReadOnlyList<Ad> pending = store.PendingApplyCheck(Today, weekAgo, limit, AllTargets);

        Assert.Equal(["finn:1", "finn:3"], pending.Select(ad => ad.Id).Order());
    }

    [Fact]
    public void ShouldListUncheckedFirstThenNewest_WhenListingApplyChecks() {
        foreach (int index in new[] { 1, 2, 3 }) {
            store.Upsert(Numbered(index) with { ApplyUrl = "https://apply.example.test/" + index });
            clock.Advance(TimeSpan.FromSeconds(1));
        }
        store.SetApplyStatus("finn:3", "active", Now.AddDays(-30));

        IReadOnlyList<Ad> pending = store.PendingApplyCheck(Today, weekAgo, 2, AllTargets);

        Assert.Equal(["finn:2", "finn:1"], pending.Select(ad => ad.Id));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("skipped")]
    public void ShouldSkipApplyRecheck_WhenStatusIsFinal(string status) {
        store.Upsert(Primary with { ApplyUrl = "https://apply.example.test/1" });
        store.SetApplyStatus(Primary.Id, status, Now.AddDays(-30));

        Assert.Empty(store.PendingApplyCheck(Today, weekAgo, limit, AllTargets));
    }

    [Fact]
    public void ShouldListAd_WhenFirstSeenAtSinceTime() {
        store.Upsert(Numbered(1));
        clock.Advance(TimeSpan.FromSeconds(1));
        store.Upsert(Numbered(2));

        Assert.Equal(["finn:2"], store.NewSince(clock.GetUtcNow()).Select(ad => ad.Id));
    }

    [Fact]
    public void ShouldListAd_WhenReportedAtSinceTime() {
        store.Upsert(Numbered(1));
        store.Upsert(Numbered(2));
        store.MarkReported(["finn:1"], Now);
        store.MarkReported(["finn:2"], Later);

        Assert.Equal(["finn:2"], store.ReportedSince(Later).Select(ad => ad.Id));
    }
}
