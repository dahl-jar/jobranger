using System.Globalization;
using System.Text.RegularExpressions;
using Jobranger.Cli.Config;
using RegexMatch = System.Text.RegularExpressions.Match;

namespace Jobranger.Cli.Features.Matching;

internal static partial class YearsGuard {
    private const RegexOptions ignoreCase = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const double oneYear = 1;
    private const int windowWords = 6;

    private static readonly Dictionary<string, int> writtenNumbers = new(StringComparer.Ordinal) {
        ["en"] = 1,
        ["ett"] = 1,
        ["to"] = 2,
        ["tre"] = 3,
        ["fire"] = 4,
        ["fem"] = 5,
        ["seks"] = 6,
        ["sju"] = 7,
        ["syv"] = 7,
        ["åtte"] = 8,
        ["ni"] = 9,
        ["ti"] = 10,
        ["one"] = 1,
        ["two"] = 2,
        ["three"] = 3,
        ["four"] = 4,
        ["five"] = 5,
        ["six"] = 6,
        ["seven"] = 7,
        ["eight"] = 8,
        ["nine"] = 9,
        ["ten"] = 10,
    };

    public static StatedYears? Find(string text) {
        if (GraduateRoute().IsMatch(text)) {
            return null;
        }
        StatedYears? best = null;
        foreach (string segment in ItemSplit().Split(text)) {
            string item = MatchText.CollapseSpace(segment);
            foreach (RegexMatch found in YearPhrase().Matches(item)) {
                if (IsOptional(item, found)) {
                    continue;
                }
                int years = Years(found);
                if (best is null || years < best.Years) {
                    best = new StatedYears(years, found.Value);
                }
            }
        }
        return best;
    }

    public static Match Apply(Match match, string title, string? description, Limits limits) {
        if (Find($"{title}\n{description ?? string.Empty}") is not { } found) {
            return match;
        }
        double years = match.RequiredExperienceYears is double model && model >= found.Years ? model : found.Years;
        if (MatchRules.MaxYears(limits) is not double cap || found.Years <= cap) {
            return match with { RequiredExperienceYears = years };
        }
        string stated = YearCount(found.Years);
        string blocker = MatchRules.Clean($"Candidate does not have {stated} of experience");
        string reason = MatchRules.Clean(
            $"The ad asks for at least {stated} of experience: \"{found.Phrase}\". "
            + $"The search accepts at most {YearCount(cap)}.");
        return match with {
            Status = Fit.Unsuitable,
            Reason = reason,
            Blockers = [.. match.Blockers.Where(item => item != blocker).Prepend(blocker).Take(MatchRules.ItemLimit)],
            RequiredExperienceYears = years,
        };
    }

    private static bool IsOptional(string item, RegexMatch found) {
        string before = string.Join(' ', Words(item[..found.Index]).TakeLast(windowWords));
        string after = string.Join(' ', Words(item[(found.Index + found.Length)..]).Take(windowWords));
        return PreferenceWord().IsMatch(before) || PreferenceWord().IsMatch(after) || EquivalentRoute().IsMatch(after);
    }

    private static string[] Words(string text) =>
        SoftHeading().Replace(text, " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string YearCount(double years) =>
        $"{MatchRules.YearsText(years)} {(years == oneYear ? "year" : "years")}";

    private static int Years(RegexMatch found) {
        string raw = found.Groups[1].Success ? found.Groups[1].Value : found.Groups[2].Value;
        if (char.IsAsciiDigit(raw[0])) {
            return int.Parse(raw, CultureInfo.InvariantCulture);
        }
        return writtenNumbers[raw.ToLowerInvariant()];
    }

    [GeneratedRegex(
        @"\b(?:(?:minimum(?: of)?|minst|at least) "
        + @"((?:[0-9]{1,2}|en|ett|to|tre|fire|fem|seks|sju|syv|åtte|ni|ti|one|two|three"
        + @"|four|five|six|seven|eight|nine|ten))"
        + @"(?: ?[-–] ?(?:[0-9]{1,2}|en|ett|to|tre|fire|fem|seks|sju|syv|åtte|ni|ti|one"
        + @"|two|three|four|five|six|seven|eight|nine|ten))?\+?"
        + @"|([0-9]{1,2}) ?(?:\+|[-–] ?[0-9]{1,2})) (?:års|år|years|year|yrs)(?:['’]|\b)"
        + @"(?: \S+){0,3}? (?:\S*erfaring|experience)",
        ignoreCase)]
    private static partial Regex YearPhrase();

    [GeneratedRegex(@"[\r\n•·▪●◦]|[.!?;](?=[ \t])")]
    private static partial Regex ItemSplit();

    [GeneratedRegex(@"\bvi ser gjerne at du(?: har)?\b", ignoreCase)]
    private static partial Regex SoftHeading();

    [GeneratedRegex(
        @"\b(?:gjerne|helst|fordel|ønskelig|preferred|preferably|desirable|advantage|a plus|nice to have)\b",
        ignoreCase)]
    private static partial Regex PreferenceWord();

    [GeneratedRegex(@"\b(?:nyutdannede?|new graduates?|recent graduates?)\b", ignoreCase)]
    private static partial Regex GraduateRoute();

    [GeneratedRegex(@"\b(?:eller tilsvarende|or equivalent)\b", ignoreCase)]
    private static partial Regex EquivalentRoute();
}
