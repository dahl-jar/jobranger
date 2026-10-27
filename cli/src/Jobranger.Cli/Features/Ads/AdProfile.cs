using System.Collections.Frozen;
using System.Text;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Ads;

internal sealed class AdProfile {
    private const int descriptionMinLength = 120;
    private const int shingleSize = 5;
    private const char space = ' ';
    private const char plus = '+';
    private const char hash = '#';

    private static readonly bool[] basicWords =
        [.. Enumerable.Range(0, char.MaxValue + 1).Select(PythonUnicode.IsWord)];
    private static readonly string[] companyCutWords = ["avd", "via"];
    private static readonly FrozenSet<string> companyNoise = FrozenSet.Create(
        StringComparer.Ordinal, [.. DedupKey.CompanySuffixes, "hf", "norge", "norway", "group", "services"]);
    private static readonly FrozenSet<string> roleTokens = FrozenSet.Create(
        StringComparer.Ordinal,
        "android", "ios", "web", "mobile", "windows", "macos", "linux", "embedded", "frontend", "backend", "fullstack",
        "java", "kotlin", "scala", "net", "c#", "f#", "c++", "c", "python", "go", "golang", "rust", "ruby", "rails",
        "php", "javascript", "typescript", "node", "react", "angular", "vue", "svelte", "swift", "flutter", "dart",
        "elixir", "erlang", "haskell", "clojure", "data", "devops", "cloud", "security", "test", "qa", "ml", "ai");
    private static readonly string[] roleNouns = [
        "utvikler", "utviklere", "developer", "developers", "ingeniør", "engineer", "arkitekt", "architect",
        "konsulent", "consultant", "programmerer", "programmer", "spesialist", "specialist", "rådgiver", "ekspert",
        "expert", "leder", "lead", "tester",
    ];

    private readonly Lazy<string> url;
    private readonly Lazy<string> description;
    private readonly Lazy<bool> longDescription;
    private readonly Lazy<string> key;
    private readonly Lazy<HashSet<string>> titleRoles;
    private readonly Lazy<string?> level;
    private readonly Lazy<string[]> descriptionWords;
    private readonly Lazy<HashSet<string>> descriptionRoles;
    private readonly Lazy<HashSet<(string, string, string, string, string)>> shingles;
    private readonly Lazy<int[]> codes;
    private readonly Lazy<Dictionary<int, int>> counts;

    public AdProfile(Ad ad) {
        Companies = CompanyTokens(ad.Company);
        Title = Normalize(ad.Title);
        Location = Normalize(ad.Location);
        LocationWords = [.. Words(Location)];
        description = new(() => Normalize(ad.Description ?? string.Empty));
        longDescription = new(() => CodePoints.Count(Description) >= descriptionMinLength);
        url = new(() => CanonicalUrls.Of(ad.Url));
        key = new(() => DedupKey.For(ad.Company, ad.Title));
        titleRoles = new(() => RoleTokens(Words(Title)));
        level = new(() => TitleLevels.Of(ad.Title));
        descriptionWords = new(() => Words(Description));
        descriptionRoles = new(() => RoleTokens(descriptionWords.Value.Distinct(StringComparer.Ordinal)));
        shingles = new(() => LongDescription ? Shingle(descriptionWords.Value) : []);
        codes = new(() => [.. CodePoints.Of(Description)]);
        counts = new(() => SequenceMatch.Counts(codes.Value));
    }

    public HashSet<string> Companies { get; }

    public string Title { get; }

    public string Location { get; }

    public HashSet<string> LocationWords { get; }

    public string Description => description.Value;

    public bool LongDescription => longDescription.Value;

    public string Url => url.Value;

    public string Key => key.Value;

    public HashSet<string> TitleRoles => titleRoles.Value;

    public string? Level => level.Value;

    public HashSet<string> DescriptionRoles => descriptionRoles.Value;

    public HashSet<(string, string, string, string, string)> Shingles => shingles.Value;

    public int[] Codes => codes.Value;

    public Dictionary<int, int> Counts => counts.Value;

    public static HashSet<string> CompanyTokens(string value) {
        string[] words = Words(Normalize(value));
        foreach (string cut in companyCutWords) {
            int index = Array.IndexOf(words, cut);
            if (index >= 0) {
                words = words[..index];
            }
        }
        return [.. words.Where(word => !companyNoise.Contains(word))];
    }

    private static HashSet<string> RoleTokens(IEnumerable<string> words) =>
        [.. words.Select(RoleToken).OfType<string>()];

    private static string? RoleToken(string word) {
        if (roleTokens.Contains(word)) {
            return word;
        }
        foreach (string noun in roleNouns) {
            if (word.Length > noun.Length && word.EndsWith(noun, StringComparison.Ordinal)
                && roleTokens.Contains(word[..^noun.Length])) {
                return word[..^noun.Length];
            }
        }
        return null;
    }

    private static HashSet<(string, string, string, string, string)> Shingle(string[] words) =>
        [.. Enumerable.Range(0, Math.Max(words.Length - shingleSize + 1, 0))
            .Select(start => (words[start], words[start + 1], words[start + 2], words[start + 3], words[start + 4]))];

    private static string[] Words(string normalized) =>
        normalized.Split(space, StringSplitOptions.RemoveEmptyEntries);

    private static string Normalize(string value) {
        StringBuilder normalized = new(value.Length);
        bool gap = false;
        foreach (int code in CodePoints.Of(PythonUnicode.CaseFold(value))) {
            if (!IsWord(code) && code != plus && code != hash) {
                gap = true;
                continue;
            }
            if (gap && normalized.Length > 0) {
                normalized.Append(space);
            }
            gap = false;
            CodePoints.Append(normalized, code);
        }
        return normalized.ToString();
    }

    private static bool IsWord(int code) => code <= char.MaxValue ? basicWords[code] : PythonUnicode.IsWord(code);
}
