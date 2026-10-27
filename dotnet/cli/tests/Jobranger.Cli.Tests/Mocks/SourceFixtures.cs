using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Sources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using CatalogueEntry = Jobranger.Cli.Catalogue.Catalogue;

namespace Jobranger.Cli.Tests.Mocks;

internal static class SourceFixtures {
    public const string NavBase = "https://pam-stilling-feed.nav.no";
    public const string NavToken = "eyJabc.def";
    public const string FinnAdBase = "https://www.finn.no/job/ad/";
    public const long NewMs = 1791331200000;
    public const long OldMs = 1791072000000;
    public const string NavUuid = "00000000-0000-4000-8000-000000000001";
    public const string SecondNavUuid = "00000000-0000-4000-8000-000000000002";
    public const string NavId = "nav:" + NavUuid;
    public const string SecondNavId = "nav:" + SecondNavUuid;

    public static readonly DateTimeOffset RunStarted = new(2026, 10, 7, 3, 0, 0, TimeSpan.Zero);

    public static readonly Targets AllRoleTargets = new() {
        Subfields = Targets.AllRoles,
        Limits = new Limits { Levels = ["junior", "mid", "none"] },
    };

    public static readonly SourcePlan Plan = CatalogueEntry.Load().PlanFor(AllRoleTargets);

    public static readonly SourcePlan BasePlan = CatalogueEntry.Load().PlanFor(new Targets());

    public static readonly EmployerFeed AshbyFeed = new("ashby", "example-one", "Org One AS", "example-one");

    public static readonly EmployerFeed TeamtailorFeed =
        new("teamtailor", "example", "Example AS", "https://careers.example.test/jobs.rss");

    public static FetchContext Context(
        HttpMessageHandler handler,
        IReadOnlySet<string>? known = null,
        IReadOnlyDictionary<string, string>? cursors = null,
        IReadOnlyList<SourceListing>? pending = null,
        SourcePlan? plan = null,
        ILogger? logger = null) => new() {
            Http = new HttpClient(handler),
            Logger = logger ?? NullLogger.Instance,
            Plan = plan ?? Plan,
            RunStarted = RunStarted,
            KnownIds = known ?? new HashSet<string>(),
            Cursors = cursors ?? new Dictionary<string, string>(),
            PendingPages = pending ?? [],
        };

    public static string SourceTestData(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Sources", name));

    public static JsonObject NavEntry(params (string Key, JsonNode? Value)[] overrides) {
        JsonObject entry = JsonNode.Parse(SourceTestData("nav_entry.json"))!.AsObject();
        JsonObject content = entry["ad_content"]!.AsObject();
        foreach ((string key, JsonNode? value) in overrides) {
            content[key] = value;
        }
        return entry;
    }

    public static JsonObject FeedItem(string uuid, string status = "ACTIVE") =>
        new() { ["_feed_entry"] = new JsonObject { ["uuid"] = uuid, ["status"] = status } };

    public static string FeedPage(JsonArray items, string? feedUrl = null, string? nextUrl = null) {
        JsonObject page = new() { ["items"] = items };
        if (feedUrl is not null) {
            page["feed_url"] = feedUrl;
        }
        if (nextUrl is not null) {
            page["next_url"] = nextUrl;
        }
        return page.ToJsonString();
    }

    public static JsonObject SearchDoc(long id, params (string Key, JsonNode? Value)[] overrides) {
        JsonObject doc = new() {
            ["id"] = id,
            ["heading"] = $"Utvikler {id}",
            ["company_name"] = "Org One AS",
            ["location"] = "Bergen",
            ["published"] = 1789394781000,
            ["deadline"] = null,
            ["canonical_url"] = $"{FinnAdBase}{id}",
        };
        foreach ((string key, JsonNode? value) in overrides) {
            doc[key] = value;
        }
        return doc;
    }

    public static JsonObject RecentDeveloperDoc(long id, params (string Key, JsonNode? Value)[] overrides) =>
        SearchDoc(id, [("heading", "Backend-utvikler"), ("job_title", "Utvikler"), ("published", NewMs), .. overrides]);

    public static string SearchHtml(IEnumerable<JsonObject> docs, int matchCount, int lastPage = 1) {
        JsonObject state = new() {
            ["queries"] = new JsonArray(new JsonObject {
                ["state"] = new JsonObject {
                    ["data"] = new JsonObject {
                        ["docs"] = new JsonArray([.. docs.Select(doc => doc.DeepClone())]),
                        ["metadata"] = new JsonObject {
                            ["result_size"] = new JsonObject { ["match_count"] = matchCount },
                            ["paging"] = new JsonObject { ["current"] = 1, ["last"] = lastPage },
                        },
                    },
                },
            }),
        };
        return StateHtml(Encoding.UTF8.GetBytes(state.ToJsonString()));
    }

    public static string StateHtml(byte[] state) =>
        $"<html><script data-react-query-state=\"1\">{Convert.ToBase64String(state)}</script></html>";

    public static string Events(FakeLogger logger) =>
        string.Join(',', logger.Collector.GetSnapshot().Select(record => record.Id.Name));

    public static string Ids(IEnumerable<SourceListing>? listings) =>
        string.Join(',', (listings ?? []).Select(listing => listing.Id));

    public static string Ids(IEnumerable<PageResult> pages) => string.Join(',', pages.Select(page => page.ListingId));

    public static SourceListing FinnListing(string id) => new() {
        Id = $"finn:{id}",
        Source = "finn",
        SourceId = id,
        Title = "Utvikler",
        Company = "Org One AS",
        Location = "Bergen",
        Url = $"{FinnAdBase}{id}",
    };
}
