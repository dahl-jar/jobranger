using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Features.Matching;

namespace Jobranger.Cli.Tests.Mocks;

internal static class DigestCases {
    private const string cardKey = "card";
    private const string casesKey = "cases";
    private const string copiesKey = "copies";
    private const string idKey = "id";

    private static readonly FrozenDictionary<string, DigestFit> fits = new Dictionary<string, DigestFit> {
        ["strong"] = DigestFit.Strong,
        ["partial"] = DigestFit.Partial,
        ["on_request"] = DigestFit.OnRequest,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, HiddenGroup> groups = new Dictionary<string, HiddenGroup> {
        ["senior"] = HiddenGroup.Senior,
        ["years"] = HiddenGroup.Years,
        ["masters"] = HiddenGroup.Masters,
        ["role"] = HiddenGroup.Role,
        ["place"] = HiddenGroup.Place,
        ["other"] = HiddenGroup.Other,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, DigestStop> stops = new Dictionary<string, DigestStop> {
        ["auth"] = DigestStop.Auth,
        ["provider_down"] = DigestStop.ProviderDown,
    }.ToFrozenDictionary();

    private static readonly JsonObject file = JsonNode.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Digest", "cases.json")))!.AsObject();

    public static DigestCard Card => ToCard(file[cardKey]!.AsObject());

    public static IReadOnlyList<string> Names() => [.. file[casesKey]!.AsObject().Select(entry => entry.Key)];

    public static (DigestData Data, string? SiteUrl) Load(string name) {
        JsonObject row = file[casesKey]![name]!.AsObject();
        DigestData data = new() {
            Day = DateOnly.Parse(Text(row, "day")!, CultureInfo.InvariantCulture),
            RanAt = DateTimeOffset.Parse(Text(row, "ran_at")!, CultureInfo.InvariantCulture),
            ApplySoon = Cards(row, "apply_soon"),
            NewToday = Cards(row, "new_today"),
            Carried = Cards(row, "carried"),
            Hidden = [.. (row["hidden"]?.AsObject() ?? []).Select(pair => new HiddenCount(groups[pair.Key], (int)pair.Value!))],
            NewAds = Number(row, "new_ads"),
            SourcesRead = Texts(row, "sources_read"),
            EmployerPages = Number(row, "employer_pages"),
            FailedSources = Texts(row, "failed_sources"),
            ClosedSinceLast = Number(row, "closed_since_last"),
            BudgetPaused = row["budget_paused"]?.GetValue<bool>() ?? false,
            Pending = Number(row, "pending"),
            MaxYears = row["max_years"]?.GetValue<double>(),
            ModelStopped = Text(row, "model_stopped") is string stop ? stops[stop] : null,
        };
        return (data, Text(row, "site_url"));
    }

    private static List<DigestCard> Cards(JsonObject row, string group) {
        List<DigestCard> cards = [];
        foreach (JsonNode? node in row[group]?.AsArray() ?? []) {
            JsonObject card = file[cardKey]!.DeepClone().AsObject();
            foreach ((string key, JsonNode? value) in node!.AsObject()) {
                card[key] = value?.DeepClone();
            }
            int? copies = card[copiesKey]?.GetValue<int>();
            if (copies is null) {
                cards.Add(ToCard(card));
                continue;
            }
            string id = Text(card, idKey)!;
            cards.AddRange(Enumerable.Range(1, copies.Value).Select(index => ToCard(card) with { Id = $"{id}-{index}" }));
        }
        return cards;
    }

    private static DigestCard ToCard(JsonObject card) {
        string? deadline = Text(card, "deadline");
        DigestFit fit = fits[Text(card, "fit")!];
        return new DigestCard {
            Id = Text(card, idKey)!,
            Source = Text(card, "source")!,
            Title = Text(card, "title")!,
            DisplayTitle = Text(card, "display_title"),
            Company = Text(card, "company")!,
            Location = Text(card, "location")!,
            Deadline = deadline,
            DeadlineDate = Matching.ParseDate(deadline),
            Url = Text(card, "url")!,
            Summary = Text(card, "summary"),
            RoleLabel = Text(card, "role_label")!,
            Fit = fit,
            IsStrongMatch = fit == DigestFit.Strong,
        };
    }

    private static string? Text(JsonObject row, string key) => row[key]?.GetValue<string>();

    private static int Number(JsonObject row, string key) => row[key]?.GetValue<int>() ?? 0;

    private static List<string> Texts(JsonObject row, string key) =>
        [.. (row[key]?.AsArray() ?? []).Select(node => node!.GetValue<string>())];
}
