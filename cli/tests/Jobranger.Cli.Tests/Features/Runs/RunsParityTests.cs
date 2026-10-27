using System.Runtime.Versioning;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Files;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using RunsEntry = Jobranger.Cli.Features.Runs.Runs;
using SourcesEntry = Jobranger.Cli.Features.Sources.Sources;

namespace Jobranger.Cli.Tests.Features.Runs;

[UnsupportedOSPlatform("windows")]
public sealed class RunsParityTests {
    private static readonly DateTimeOffset runStart = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly string inputs = Path.Combine(AppContext.BaseDirectory, "TestData", "RunPass");
    private static readonly JsonSerializerOptions jsonOptions = new() {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Fact]
    public async Task ShouldStoreSameRows_WhenPythonRanSameInput() {
        using TestDatabase db = new();
        using TempDir dir = new();
        Paths paths = RunFixtures.TempPaths(dir);
        FakeTimeProvider time = new(runStart);
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        AppConfig config = ConfigFile.Load(Path.Combine(inputs, "config.yaml"), dir.Path) with {
            Profile = new ProfileRef(MatchingFixtures.TestData("profile_user.yaml")),
        };
        using HttpClient http = new(SiteRoutes.Load(Path.Combine(inputs, "routes.json")));
        string repliesJson = File.ReadAllText(Path.Combine(inputs, "replies.json"));
        FakeModelClient client = FakeModelClient.Keyed(Strings(JsonNode.Parse(repliesJson)!));
        RunsEntry runs = new(
            db.Connection, paths, http, (_, _, _) => Task.FromResult<Jobranger.Cli.Features.Providers.IModelClient>(client),
            SourcesEntry.Enabled, time, NullLogger.Instance);

        RunResult result = await runs.RunAsync(
            new RunRequest(config, new RecordingRunEvents()), TestContext.Current.CancellationToken);
        string actual = Projection(db, result).ToJsonString(jsonOptions);

        Assert.Equal(MatchingFixtures.GoldenText("RunPass", "rows.json").TrimEnd(), actual);
    }

    private static Dictionary<string, string> Strings(JsonNode node) =>
        node.AsObject().ToDictionary(entry => entry.Key, entry => entry.Value!.GetValue<string>(), StringComparer.Ordinal);

    private static JsonObject Projection(TestDatabase db, RunResult result) {
        JsonObject tables = [];
        foreach ((string name, string sql) in Strings(JsonNode.Parse(File.ReadAllText(Path.Combine(inputs, "queries.json")))!)) {
            tables[name] = new JsonArray([.. TestDatabase.Rows(db.Connection, sql).Select(row => (JsonNode)row)]);
        }
        JsonObject hidden = [];
        foreach ((HiddenKind kind, int count) in result.HiddenCounts.OrderBy(entry => WireNames.Of(entry.Key), StringComparer.Ordinal)) {
            hidden[WireNames.Of(kind)] = count;
        }
        return new JsonObject {
            ["tables"] = tables,
            ["result"] = new JsonObject {
                ["status"] = WireNames.Of(result.Status),
                ["reported_ids"] = Array(result.ReportedIds),
                ["carried_ids"] = Array(result.CarriedIds),
                ["silent_ids"] = Array(result.SilentIds),
                ["hidden_counts"] = hidden,
                ["sources_read"] = Array(result.SourcesRead.Order(StringComparer.Ordinal)),
                ["failed_sources"] = Array(result.FailedSources.Order(StringComparer.Ordinal)),
                ["pending"] = result.Pending,
                ["model_stopped"] = StopName(result.ModelStopped),
            },
        };
    }

    private static JsonArray Array(IEnumerable<string> ids) => new([.. ids.Select(id => (JsonNode)id)]);

    private static string? StopName(StopReason? reason) => reason switch {
        null => null,
        StopReason.ProviderDown => "provider_down",
        StopReason stopped => WireNames.Of(stopped),
    };
}
