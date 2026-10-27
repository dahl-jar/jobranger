using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jobranger.Cli.Config;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Catalogue;

internal sealed class Catalogue {
    public const string GeneralSoftware = "general_software";

    public static readonly IReadOnlySet<string> SeniorLevels =
        new HashSet<string>(StringComparer.Ordinal) { TitleLevels.Senior, TitleLevels.Lead };

    public static readonly IReadOnlyDictionary<string, string> LevelsFromLabel =
        new Dictionary<string, string>(StringComparer.Ordinal) {
            ["junior"] = "junior",
            ["mid"] = "mid",
            ["senior"] = "senior",
            ["lead"] = "lead",
            ["none"] = "none",
            ["error"] = "none",
        };

    private const string resourceName = "catalogue.json";
    private const string baseKey = "base";
    private const string rolesKey = "roles";
    private const string acronymsKey = "acronyms";
    private const string genericWordsKey = "generic_words";
    private const string roleNounsKey = "role_nouns";
    private const string nonSoftwarePrefixesKey = "non_software_prefixes";
    private const string employersKey = "employers";
    private const string labelKey = "label";
    private const string linkedInKeywordsKey = "linkedin_keywords";
    private const string navStyrkKey = "nav_styrk";
    private const string navCategoriesKey = "nav_categories";
    private const string finnOccupationsKey = "finn_occupations";
    private const string titleWordsKey = "title_words";
    private const string kindKey = "kind";
    private const string feedKey = "key";
    private const string companyKey = "company";
    private const string boardKey = "board";
    private const string wordStart = @"(?<!\w)";
    private const string wordEnd = @"(?!\w)";
    private const string nounEndings = "(?:e|en|ne|er|ene|s)?";
    private const char alternative = '|';
    private const string otherRoleLabel = "Other";

    private static readonly TimeSpan matchTimeout = TimeSpan.FromSeconds(1);
    private static readonly Lazy<Catalogue> loaded = new(Read);

    private readonly RoleEntry baseEntry;
    private readonly IReadOnlyList<(string Id, RoleEntry Entry)> roles;
    private readonly string acronyms;
    private readonly string compounds;
    private readonly IReadOnlyList<EmployerFeed> employers;
    private readonly Regex generic;
    private readonly Regex developer;

    private Catalogue(
        RoleEntry baseEntry,
        IReadOnlyList<(string Id, RoleEntry Entry)> roles,
        IReadOnlyList<string> acronyms,
        IReadOnlyList<string> genericWords,
        IReadOnlyList<string> roleNouns,
        IReadOnlyList<string> nonSoftwarePrefixes,
        IReadOnlyList<EmployerFeed> employers) {
        this.baseEntry = baseEntry;
        this.roles = roles;
        this.acronyms = string.Join(alternative, acronyms.Select(Regex.Escape));
        compounds = Compounds(roleNouns, nonSoftwarePrefixes);
        this.employers = employers;
        generic = Compile(IgnoringCase(genericWords));
        developer = Compile(IgnoringCase(Union([baseEntry, .. roles.Select(role => role.Entry)], entry => entry.TitleWords)));
    }

    public static Catalogue Load() => loaded.Value;

    public SourcePlan PlanFor(Targets targets) {
        RoleEntry[] entries = [
            baseEntry,
            .. targets.Subfields.SelectMany(id => roles.Where(role => role.Id == id).Select(role => role.Entry)),
        ];
        Regex plan = Compile($@"{IgnoringCase(Union(entries, entry => entry.TitleWords))}|{compounds}|\b(?:{acronyms})\b");
        return new SourcePlan(plan, generic, developer) {
            LinkedInKeywords = Union(entries, entry => entry.LinkedInKeywords),
            NavStyrk = Union(entries, entry => entry.NavStyrk),
            NavCategories = entries.SelectMany(entry => entry.NavCategories).ToHashSet(),
            FinnOccupations = Union(entries, entry => entry.FinnOccupations),
            SkipTitleLevels = SeniorLevels.Except(targets.Limits.Levels, StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal),
        };
    }

    public IReadOnlyList<(string Id, string Label)> RoleList() => [.. roles.Select(role => (role.Id, role.Entry.Label))];

    public string RoleLabel(string? role) => role == GeneralSoftware
        ? baseEntry.Label
        : roles.Where(entry => entry.Id == role).Select(entry => entry.Entry.Label).FirstOrDefault() ?? otherRoleLabel;

    public IReadOnlyList<EmployerFeed> DefaultEmployerFeeds() => employers;

    private static IReadOnlyList<string> Union(IEnumerable<RoleEntry> entries, Func<RoleEntry, IReadOnlyList<string>> pick) =>
        [.. entries.SelectMany(pick).Distinct(StringComparer.Ordinal)];

    private static string IgnoringCase(IEnumerable<string> words) =>
        $"(?i:{string.Join(alternative, words.Select(Anchored))})";

    private static string Compounds(IReadOnlyList<string> nouns, IReadOnlyList<string> nonSoftwarePrefixes) {
        string noun = $"(?:{Alternatives(nouns)}){nounEndings}{wordEnd}";
        return $@"(?i:{wordStart}(?!(?:{Alternatives(nonSoftwarePrefixes)}){noun})\w*?{noun})";
    }

    private static string Alternatives(IEnumerable<string> words) =>
        string.Join(alternative, words.Select(word => Regex.Escape(word.ToLowerInvariant())));

    private static string Anchored(string word) {
        string escaped = Regex.Escape(word.ToLowerInvariant());
        return char.IsLetterOrDigit(word[0]) ? wordStart + escaped : escaped;
    }

    private static Regex Compile(string pattern) => new(pattern, RegexOptions.CultureInvariant, matchTimeout);

    private static Catalogue Read() {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!;
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        return new Catalogue(
            Entry(root.GetProperty(baseKey)),
            [.. root.GetProperty(rolesKey).EnumerateObject().Select(role => (role.Name, Entry(role.Value)))],
            Strings(root.GetProperty(acronymsKey)),
            Strings(root.GetProperty(genericWordsKey)),
            Strings(root.GetProperty(roleNounsKey)),
            Strings(root.GetProperty(nonSoftwarePrefixesKey)),
            [.. root.GetProperty(employersKey).EnumerateArray().Select(Feed)]);
    }

    private static RoleEntry Entry(JsonElement entry) => new(
        entry.GetProperty(labelKey).GetString()!,
        Strings(entry.GetProperty(linkedInKeywordsKey)),
        Strings(entry.GetProperty(navStyrkKey)),
        [.. entry.GetProperty(navCategoriesKey).EnumerateArray().Select(pair => (pair[0].GetString()!, pair[1].GetString()!))],
        Strings(entry.GetProperty(finnOccupationsKey)),
        Strings(entry.GetProperty(titleWordsKey)));

    private static EmployerFeed Feed(JsonElement feed) => new(
        feed.GetProperty(kindKey).GetString()!,
        feed.GetProperty(feedKey).GetString()!,
        feed.GetProperty(companyKey).GetString()!,
        feed.GetProperty(boardKey).GetString()!);

    private static IReadOnlyList<string> Strings(JsonElement list) =>
        [.. list.EnumerateArray().Select(item => item.GetString()!)];
}
