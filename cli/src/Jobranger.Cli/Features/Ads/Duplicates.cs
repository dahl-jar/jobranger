using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Ads;

internal static class Duplicates {
    private const double vacancyJaccardThreshold = 0.9;
    private const double descriptionSimilarityThreshold = 0.92;
    private const double unrelatedDescriptionThreshold = 0.5;
    private const int minCompanyTokenLength = 3;

    public static IReadOnlyList<IReadOnlyList<Ad>> Group(IReadOnlyList<Ad> ads) {
        List<List<Ad>> groups = [];
        List<AdProfile> heads = [];
        Dictionary<string, List<int>> byUrl = [];
        Dictionary<string, List<int>> byKey = [];
        Dictionary<string, List<int>> byCompany = [];
        foreach (Ad ad in ads.OrderBy(ad => ad.Id, StringComparer.Ordinal)) {
            AdProfile profile = new(ad);
            IEnumerable<int> candidates = Candidates(profile, heads, byUrl, byKey, byCompany);
            int match = candidates.Order().Distinct().FirstOrDefault(index => IsDuplicate(profile, heads[index]), -1);
            if (match >= 0) {
                groups[match].Add(ad);
                continue;
            }
            int created = groups.Count;
            groups.Add([ad]);
            heads.Add(profile);
            if (profile.Url.Length > 0) {
                Index(byUrl, profile.Url, created);
            }
            Index(byKey, profile.Key, created);
            foreach (string token in profile.Companies) {
                Index(byCompany, token, created);
            }
        }
        return groups;
    }

    public static bool IsSameVacancy(Ad left, Ad right) => IsSameVacancy(new AdProfile(left), new AdProfile(right));

    public static (IReadOnlyList<Ad> Remaining, IReadOnlyList<string> Covered) SplitReported(
        IReadOnlyList<Ad> ads, IReadOnlyList<Ad> reported) {
        AdProfile[] reportedProfiles = [.. reported.Select(ad => new AdProfile(ad))];
        List<Ad> remaining = [];
        List<string> covered = [];
        foreach (Ad ad in ads) {
            AdProfile profile = new(ad);
            if (reportedProfiles.Any(other => IsSameVacancy(profile, other))) {
                covered.Add(ad.Id);
            } else {
                remaining.Add(ad);
            }
        }
        return (remaining, covered);
    }

    public static bool CompaniesMatch(string left, string right) =>
        CompaniesMatch(AdProfile.CompanyTokens(left), AdProfile.CompanyTokens(right));

    private static IEnumerable<int> Candidates(
        AdProfile profile,
        List<AdProfile> heads,
        Dictionary<string, List<int>> byUrl,
        Dictionary<string, List<int>> byKey,
        Dictionary<string, List<int>> byCompany) {
        Dictionary<int, int> shared = [];
        foreach (string token in profile.Companies) {
            foreach (int group in byCompany.GetValueOrDefault(token) ?? []) {
                shared[group] = shared.GetValueOrDefault(group) + 1;
            }
        }
        IEnumerable<int> candidates = shared
            .Where(entry => entry.Value == profile.Companies.Count || entry.Value == heads[entry.Key].Companies.Count)
            .Select(entry => entry.Key)
            .Concat(byKey.GetValueOrDefault(profile.Key) ?? []);
        return profile.Url.Length > 0 ? candidates.Concat(byUrl.GetValueOrDefault(profile.Url) ?? []) : candidates;
    }

    private static void Index(Dictionary<string, List<int>> index, string key, int group) {
        if (!index.TryGetValue(key, out List<int>? groups)) {
            groups = [];
            index[key] = groups;
        }
        groups.Add(group);
    }

    private static bool CompaniesMatch(HashSet<string> leftTokens, HashSet<string> rightTokens) {
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

    private static bool IsDuplicate(AdProfile left, AdProfile right) =>
        (left.Url.Length > 0 && left.Url == right.Url) || IsSameVacancy(left, right) || IsNearCopy(left, right);

    private static bool IsSameVacancy(AdProfile left, AdProfile right) {
        if (!LocationsOverlap(left, right)) {
            return false;
        }
        if (left.Key == right.Key) {
            return !DescriptionsUnrelated(left, right);
        }
        if (!CompaniesMatch(left.Companies, right.Companies)) {
            return false;
        }
        if (left.Title == right.Title) {
            return !DescriptionsUnrelated(left, right);
        }
        return SameRole(left, right) && ShingleJaccard(left, right) >= vacancyJaccardThreshold;
    }

    private static bool IsNearCopy(AdProfile left, AdProfile right) {
        if (!BothLong(left, right) || left.Location != right.Location
            || !CompaniesMatch(left.Companies, right.Companies) || !SameRole(left, right)) {
            return false;
        }
        return left.Description == right.Description || RatioAtLeast(left, right, descriptionSimilarityThreshold);
    }

    private static bool DescriptionsUnrelated(AdProfile left, AdProfile right) =>
        BothLong(left, right) && !RatioAtLeast(left, right, unrelatedDescriptionThreshold);

    private static bool RatioAtLeast(AdProfile left, AdProfile right, double threshold) =>
        SequenceMatch.QuickRatio(left.Counts, right.Counts) >= threshold
        && SequenceMatch.Ratio(left.Codes, right.Codes) >= threshold;

    private static bool BothLong(AdProfile left, AdProfile right) => left.LongDescription && right.LongDescription;

    private static bool SameRole(AdProfile left, AdProfile right) {
        bool titled = left.TitleRoles.Count > 0 || right.TitleRoles.Count > 0;
        HashSet<string> leftRoles = titled ? left.TitleRoles : left.DescriptionRoles;
        HashSet<string> rightRoles = titled ? right.TitleRoles : right.DescriptionRoles;
        return leftRoles.SetEquals(rightRoles) && left.Level == right.Level;
    }

    private static bool LocationsOverlap(AdProfile left, AdProfile right) =>
        left.LocationWords.Count == 0 || right.LocationWords.Count == 0
        || left.LocationWords.Overlaps(right.LocationWords);

    private static double ShingleJaccard(AdProfile left, AdProfile right) {
        HashSet<(string, string, string, string, string)> leftShingles = left.Shingles;
        HashSet<(string, string, string, string, string)> rightShingles = right.Shingles;
        if (leftShingles.Count == 0 || rightShingles.Count == 0) {
            return 0.0;
        }
        bool leftSmaller = leftShingles.Count <= rightShingles.Count;
        HashSet<(string, string, string, string, string)> smaller = leftSmaller ? leftShingles : rightShingles;
        HashSet<(string, string, string, string, string)> larger = leftSmaller ? rightShingles : leftShingles;
        int shared = smaller.Count(larger.Contains);
        return (double)shared / (leftShingles.Count + rightShingles.Count - shared);
    }
}
