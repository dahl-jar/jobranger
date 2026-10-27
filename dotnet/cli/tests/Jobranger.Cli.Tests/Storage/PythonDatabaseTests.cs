using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Applications;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using ApplicationsEntry = Jobranger.Cli.Features.Applications.Applications;

namespace Jobranger.Cli.Tests.Storage;

[UnsupportedOSPlatform("windows")]
public sealed class PythonDatabaseTests : IDisposable {
    private const int runLimit = 10;

    private static readonly JsonElement golden = LoadGolden();

    private readonly TempDir temp = new();
    private readonly SqliteConnection connection;
    private readonly AdStore ads;

    public PythonDatabaseTests() {
        string path = temp.File("python-v2.db");
        File.Copy(TestDatabase.PythonDatabase(), path);
        connection = Database.Open(path, NullLogger.Instance);
        ads = new AdStore(connection, new FakeTimeProvider());
    }

    public void Dispose() {
        connection.Dispose();
        temp.Dispose();
    }

    [Fact]
    public void ShouldReadEveryRow_WhenDatabaseCameFromPythonCli() {
        IReadOnlyList<Ad> read = ads.ById(ads.KnownIds());

        Assert.Equal(Rows("ads", "dedup_key"), read.Select(AdFields));
    }

    [Fact]
    public void ShouldBuildSameDedupKey_WhenAdCameFromPythonCli() {
        IEnumerable<string> expected = Golden("ads").EnumerateArray().Select(row => row.GetProperty("dedup_key").GetString()!);

        Assert.Equal(expected, ads.ById(ads.KnownIds()).Select(ad => DedupKey.For(ad.Company, ad.Title)));
    }

    [Fact]
    public void ShouldReadEveryAction_WhenDatabaseCameFromPythonCli() {
        Dictionary<string, string> expected = Golden("actions").EnumerateObject().ToDictionary(
            item => item.Name, item => string.Join('|', item.Value.EnumerateArray().Select(name => name.GetString())));

        Dictionary<string, string> read = ads.Actions().ToDictionary(
            item => item.Key, item => string.Join('|', item.Value.Select(ActionName).Order(StringComparer.Ordinal)));

        Assert.Equal(expected, read);
    }

    [Fact]
    public void ShouldReadEveryClosedAd_WhenDatabaseCameFromPythonCli() {
        DateOnly asOf = Day(Golden("closed_as_of"));

        IReadOnlyList<ClosedAd> closed = ads.Closed(asOf);

        Assert.Equal(Rows("closed"), closed.Select(item => Fields(
            ("id", item.Ad.Id),
            ("closed_on", item.ClosedOn),
            ("why", Snake(item.Reason.ToString())),
            ("you", item.Action is AdAction action ? ActionName(action) : null))));
    }

    [Fact]
    public void ShouldReadEveryApplication_WhenDatabaseCameFromPythonCli() {
        IReadOnlyList<Application> read = new ApplicationsEntry(connection).List(includeRemoved: true);

        Assert.Equal(Rows("applications"), read.Select(application => Fields(
            ("id", application.Id),
            ("ad_id", application.AdId),
            ("company", application.Company),
            ("role", application.Role),
            ("place", application.Place),
            ("deadline", application.Deadline),
            ("status", Snake(application.Status.ToString())),
            ("note", application.Note),
            ("applied_at", application.AppliedAt),
            ("cv_name", application.CvName),
            ("removed_at", application.RemovedAt),
            ("events", application.Events.Select(item => GoldenJson.Object(("at", item.At), ("text", item.Text)))))));
    }

    [Fact]
    public void ShouldReadEveryRun_WhenDatabaseCameFromPythonCli() {
        IReadOnlyList<RunRow> read = new RunStore(connection).List(runLimit);

        Assert.Equal(Rows("runs"), read.Select(run => Fields(
            ("id", run.Id),
            ("started_at", run.StartedAt),
            ("finished_at", run.FinishedAt),
            ("status", Snake(run.Status.ToString())),
            ("ads_read", run.AdsRead),
            ("new_ads", run.NewAds),
            ("matches", run.Matches),
            ("hidden", run.Hidden),
            ("failed_sources", run.FailedSources))));
    }

    [Fact]
    public void ShouldReadEveryUsageDay_WhenDatabaseCameFromPythonCli() {
        DateOnly since = Day(Golden("usage_since"));

        IReadOnlyList<UsageDay> read = new UsageStore(connection).Days(since);

        Assert.Equal(Rows("usage_days"), read.Select(day => Fields(
            ("day", day.Day),
            ("calls", day.Calls),
            ("input_tokens", day.InputTokens),
            ("output_tokens", day.OutputTokens),
            ("cost_usd", day.CostUsd),
            ("ads_read", day.AdsRead),
            ("ads_checked", day.AdsChecked))));
    }

    [Fact]
    public void ShouldReadSameSpend_WhenDatabaseCameFromPythonCli() {
        JsonElement spend = Golden("spend");
        DateOnly start = Day(spend.GetProperty("start"));
        DateOnly end = Day(spend.GetProperty("end"));

        double read = new UsageStore(connection).SpendBetween(start, end);

        Assert.Equal(spend.GetProperty("usd").GetDouble(), read);
    }

    [Fact]
    public void ShouldReadEveryCursor_WhenDatabaseCameFromPythonCli() {
        Dictionary<string, string> expected =
            Golden("cursors").EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetString()!);

        IReadOnlyDictionary<string, string> read = new StateStore(connection).Cursors();

        Assert.Equal(expected, read);
    }

    private static JsonElement LoadGolden() {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "Store", "rows.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static JsonElement Golden(string name) => golden.GetProperty(name);

    private static DateOnly Day(JsonElement value) => DateOnly.Parse(value.GetString()!, CultureInfo.InvariantCulture);

    private static IEnumerable<string> Rows(string name, string? skipped = null) =>
        Golden(name).EnumerateArray().Select(row => GoldenJson.Canonical(row, skipped));

    private static string AdFields(Ad ad) => Fields(
        ("id", ad.Id),
        ("source", ad.Source),
        ("source_id", ad.SourceId),
        ("title", ad.Title),
        ("company", ad.Company),
        ("location", ad.Location),
        ("published", ad.Published),
        ("deadline", ad.Deadline),
        ("url", ad.Url),
        ("description", ad.Description),
        ("label", ad.Label),
        ("summary", ad.Summary),
        ("requires_master", ad.RequiresMaster),
        ("is_programming_job", ad.IsProgrammingJob),
        ("display_title", ad.DisplayTitle),
        ("role_type", ad.RoleType),
        ("technologies", ad.Technologies),
        ("requirements", ad.Requirements),
        ("assessment", ad.Assessment),
        ("assessment_version", ad.AssessmentVersion),
        ("apply_url", ad.ApplyUrl),
        ("apply_status", ad.ApplyStatus),
        ("apply_checked_at", ad.ApplyCheckedAt),
        ("republished", ad.Republished),
        ("first_seen", ad.FirstSeen),
        ("closed_at", ad.ClosedAt),
        ("first_classified_at", ad.FirstClassifiedAt));

    private static string Fields(params (string Name, object? Value)[] fields) => GoldenJson.Row(fields);

    private static string ActionName(AdAction action) => Snake(action.ToString());

    private static string Snake(string pascal) => string.Concat(pascal.Select((character, index) =>
        char.IsUpper(character) && index > 0 ? "_" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString()));
}
