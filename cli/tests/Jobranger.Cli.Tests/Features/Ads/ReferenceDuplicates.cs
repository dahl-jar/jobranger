using System.Collections.Frozen;
using System.Text;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Tests.Text;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Features.Ads;

internal static class ReferenceDuplicates {
    private const int descriptionMinLength = 120;
    private const double vacancyJaccardThreshold = 0.9;
    private const double descriptionSimilarityThreshold = 0.92;
    private const double unrelatedDescriptionThreshold = 0.5;
    private const int shingleSize = 5;
    private const int minCompanyTokenLength = 3;
    private const char space = ' ';
    private const char plus = '+';
    private const char hash = '#';

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

    public static IReadOnlyList<IReadOnlyList<Ad>> Group(IReadOnlyList<Ad> ads) {
        List<List<Ad>> groups = [];
        foreach (Ad ad in ads.OrderBy(ad => ad.Id, StringComparer.Ordinal)) {
            List<Ad>? group = groups.Find(candidate => IsDuplicate(ad, candidate[0]));
            if (group is null) {
                groups.Add([ad]);
            } else {
                group.Add(ad);
            }
        }
        return groups;
    }

    public static bool IsSameVacancy(Ad left, Ad right) {
        if (!LocationsOverlap(left.Location, right.Location)) {
            return false;
        }
        if (DedupKey.For(left.Company, left.Title) == DedupKey.For(right.Company, right.Title)) {
            return !DescriptionsUnrelated(left, right);
        }
        if (!CompaniesMatch(left.Company, right.Company)) {
            return false;
        }
        string leftTitle = Normalize(left.Title);
        string rightTitle = Normalize(right.Title);
        if (leftTitle == rightTitle) {
            return !DescriptionsUnrelated(left, right);
        }
        if (!SameRole(left, right)) {
            return false;
        }
        return ShingleJaccard(left.Description ?? string.Empty, right.Description ?? string.Empty)
            >= vacancyJaccardThreshold;
    }

    public static (IReadOnlyList<Ad> Remaining, IReadOnlyList<string> Covered) SplitReported(
        IReadOnlyList<Ad> ads, IReadOnlyList<Ad> reported) {
        List<Ad> remaining = [];
        List<string> covered = [];
        foreach (Ad ad in ads) {
            if (reported.Any(other => IsSameVacancy(ad, other))) {
                covered.Add(ad.Id);
            } else {
                remaining.Add(ad);
            }
        }
        return (remaining, covered);
    }

    public static bool CompaniesMatch(string left, string right) {
        HashSet<string> leftTokens = CompanyTokens(left);
        HashSet<string> rightTokens = CompanyTokens(right);
        if (leftTokens.Count == 0 || rightTokens.Count == 0) {
            return false;
        }
        if (leftTokens.SetEquals(rightTokens)) {
            return true;
        }
        HashSet<string> smaller = rightTokens.Count < leftTokens.Count ? rightTokens : leftTokens;
        HashSet<string> larger = smaller == leftTokens ? rightTokens : leftTokens;
        return smaller.IsSubsetOf(larger)
            && smaller.Any(token => CodePoints.Count(token) >= minCompanyTokenLength);
    }

    private static bool IsDuplicate(Ad left, Ad right) =>
        SameUrl(left.Url, right.Url) || IsSameVacancy(left, right) || IsNearCopy(left, right);

    private static bool SameUrl(string left, string right) {
        string canonical = CanonicalUrls.Of(left);
        return canonical.Length > 0 && canonical == CanonicalUrls.Of(right);
    }

    private static bool IsNearCopy(Ad left, Ad right) {
        if (!CompaniesMatch(left.Company, right.Company)
            || Normalize(left.Location) != Normalize(right.Location)
            || !SameRole(left, right)) {
            return false;
        }
        string leftText = Description(left);
        string rightText = Description(right);
        return BothLong(leftText, rightText)
            && (leftText == rightText || ReferenceSequenceMatch.Ratio(leftText, rightText) >= descriptionSimilarityThreshold);
    }

    private static bool DescriptionsUnrelated(Ad left, Ad right) {
        string leftText = Description(left);
        string rightText = Description(right);
        return BothLong(leftText, rightText)
            && ReferenceSequenceMatch.Ratio(leftText, rightText) < unrelatedDescriptionThreshold;
    }

    private static bool BothLong(string left, string right) =>
        CodePoints.Count(left) >= descriptionMinLength && CodePoints.Count(right) >= descriptionMinLength;

    private static string Description(Ad ad) => Normalize(ad.Description ?? string.Empty);

    private static bool SameRole(Ad left, Ad right) {
        HashSet<string> leftRoles = RoleTokens(Normalize(left.Title));
        HashSet<string> rightRoles = RoleTokens(Normalize(right.Title));
        if (leftRoles.Count == 0 && rightRoles.Count == 0) {
            leftRoles = RoleTokens(Description(left));
            rightRoles = RoleTokens(Description(right));
        }
        return leftRoles.SetEquals(rightRoles) && TitleLevels.Of(left.Title) == TitleLevels.Of(right.Title);
    }

    private static HashSet<string> RoleTokens(string normalized) =>
        [.. Words(normalized).Select(RoleToken).OfType<string>()];

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

    private static bool LocationsOverlap(string left, string right) {
        HashSet<string> leftWords = [.. Words(Normalize(left))];
        HashSet<string> rightWords = [.. Words(Normalize(right))];
        return leftWords.Count == 0 || rightWords.Count == 0 || leftWords.Overlaps(rightWords);
    }

    private static double ShingleJaccard(string left, string right) {
        HashSet<string> leftShingles = Shingles(left);
        HashSet<string> rightShingles = Shingles(right);
        if (leftShingles.Count == 0 || rightShingles.Count == 0) {
            return 0.0;
        }
        int shared = leftShingles.Count(rightShingles.Contains);
        return (double)shared / (leftShingles.Count + rightShingles.Count - shared);
    }

    private static HashSet<string> Shingles(string text) {
        string normalized = Normalize(text);
        if (CodePoints.Count(normalized) < descriptionMinLength) {
            return [];
        }
        string[] words = Words(normalized);
        return [.. Enumerable.Range(0, Math.Max(words.Length - shingleSize + 1, 0))
            .Select(start => string.Join(space, words, start, shingleSize))];
    }

    private static HashSet<string> CompanyTokens(string value) {
        string[] words = Words(Normalize(value));
        foreach (string cut in companyCutWords) {
            int index = Array.IndexOf(words, cut);
            if (index >= 0) {
                words = words[..index];
            }
        }
        return [.. words.Where(word => !companyNoise.Contains(word))];
    }

    private static string[] Words(string normalized) =>
        normalized.Split(space, StringSplitOptions.RemoveEmptyEntries);

    private static string Normalize(string value) {
        StringBuilder normalized = new(value.Length);
        bool gap = false;
        foreach (int code in CodePoints.Of(PythonUnicode.CaseFold(value))) {
            if (!PythonUnicode.IsWord(code) && code != plus && code != hash) {
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
}
