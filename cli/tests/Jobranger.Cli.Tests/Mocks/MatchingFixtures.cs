using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Matching;

namespace Jobranger.Cli.Tests.Mocks;

internal static class MatchingFixtures {
    public const string Version = "test-version";
    public const string OutdatedVersion = "old";
    public const string Guidance = "Judge roles by the actual work.\n\nPrefer backend roles.";

    public static readonly Limits Narrow = new() { Levels = ["junior", "mid", "none"], Years = ["0-1"], HideMasters = true };

    public static readonly Limits Wide = new() { Levels = ["junior", "senior"], Years = ["0-1", "6+"], HideMasters = false };

    public static readonly IReadOnlyDictionary<string, Limits> LimitVariants = new Dictionary<string, Limits> {
        ["narrow"] = Narrow,
        ["wide"] = Wide,
        ["lead"] = new() { Levels = ["mid", "lead"], Years = ["1-3", "3-6"], HideMasters = true },
        ["none"] = new() { Levels = ["none"], Years = [], HideMasters = false },
    };

    public static readonly IReadOnlyDictionary<string, string> Guidances = new Dictionary<string, string> {
        ["plain"] = Guidance,
        ["accents"] = "Foretrekk roller i Bergen – særlig backend.\nIgnore \"senior\" i tittelen.",
    };

    public static readonly Match StrongMatch = new(
        "strong",
        "Java project demonstrates Java and Spring Boot service development.",
        ["java_project"],
        [],
        [],
        IsLowCode: false,
        RequiredExperienceYears: null,
        Version);

    public static readonly IReadOnlySet<string> EvidenceIds = new HashSet<string>(StringComparer.Ordinal) {
        "education", "work_frontend", "work_data", "java_project", "cloud_project", "django_project",
        "android_project", "cpp_project",
    };

    public static readonly IReadOnlySet<string> EducationIds = new HashSet<string>(StringComparer.Ordinal) { "education" };

    public static readonly AdView Ad = new() {
        Id = "finn:1234",
        Source = "finn",
        Title = "Utvikler",
        Company = "Org One AS",
        Location = "Bergen",
        Published = "2026-08-24T00:00:00+00:00",
        IsProgrammingJob = true,
        DisplayTitle = "Software Developer",
        RoleType = "general_software",
        Match = StrongMatch,
    };

    public static Targets AllRoles(Limits? limits = null) => new() { Subfields = Targets.AllRoles, Limits = limits ?? Narrow };

    public static string TestData(string name) => Path.Combine(AppContext.BaseDirectory, "TestData", "Matching", name);

    public static string GoldenText(params string[] parts) =>
        File.ReadAllText(Path.Combine([AppContext.BaseDirectory, "Golden", .. parts]), Encoding.UTF8);

    public static JsonObject Payload() => JsonNode.Parse(File.ReadAllText(TestData("classification.json")))!.AsObject();

    public static JsonObject Merge(JsonObject target, string overrides) => Merge(target, JsonNode.Parse(overrides)!.AsObject());

    private static JsonObject Merge(JsonObject target, JsonObject overrides) {
        foreach ((string key, JsonNode? value) in overrides.ToList()) {
            if (value is JsonObject nested && target[key] is JsonObject existing) {
                Merge(existing, nested);
            } else {
                target[key] = value?.DeepClone();
            }
        }
        return target;
    }
}
