using System.Globalization;
using Jobranger.Cli.Config;
using Jobranger.Cli.Text;
using RoleCatalogue = Jobranger.Cli.Catalogue.Catalogue;

namespace Jobranger.Cli.Features.Matching;

internal static class MatchRules {
    private const int textLimit = 300;
    private const int itemLimit = 5;
    private const string noYearCapRule = "The search accepts roles requiring any number of years.";
    private const string seniorUnsuitableRule =
        "Senior and lead roles are unsuitable, even with skill fit; name the level as a blocker.";
    private const string masterRule =
        "Mandatory completed master's without a bachelor/experience alternative is unsuitable; name it as a blocker.";
    private const string levelSeparator = ", ";
    private const string shortFormat = "G6";
    private const char upperExponent = 'E';
    private const char lowerExponent = 'e';
    private const double noYears = 0;

    private static readonly Dictionary<string, string> levelNames =
        new(StringComparer.Ordinal) {
            ["junior"] = "Junior",
            ["mid"] = "Mid-level",
            ["senior"] = "Senior",
            ["lead"] = "Tech lead",
            ["none"] = "No level stated",
        };

    private static readonly IReadOnlyList<(string Band, double? Cap)> yearBandCaps = [
        ("0-1", 1), ("1-3", 3), ("3-6", 6), ("6+", null),
    ];

    public static double? MaxYears(Limits limits) {
        IEnumerable<double?> caps = yearBandCaps.Where(entry => limits.Years.Contains(entry.Band)).Select(entry => entry.Cap);
        if (caps.Any(cap => cap is null)) {
            return null;
        }
        return caps.Select(cap => cap!.Value).DefaultIfEmpty(noYears).Max();
    }

    public static string BandOf(double years) =>
        yearBandCaps.First(entry => entry.Cap is null || years <= entry.Cap).Band;

    public static string LimitRules(Limits limits) {
        double? cap = MaxYears(limits);
        List<string> lines = [cap is double years ? YearCapRule(YearsText(years)) : noYearCapRule];
        if (!limits.Levels.Any(RoleCatalogue.SeniorLevels.Contains)) {
            lines.Add(seniorUnsuitableRule);
        } else {
            IEnumerable<string> names = Limits.AllLevels.Where(limits.Levels.Contains).Select(level => levelNames[level]);
            lines.Add($"The search accepts these levels: {string.Join(levelSeparator, names)}.");
        }
        if (limits.HideMasters) {
            lines.Add(masterRule);
        }
        return string.Join('\n', lines);
    }

    public static Match? Parse(MatchAssessment raw, string version) {
        if (raw.RequiredExperienceYears is double count && (!double.IsFinite(count) || count < 0)) {
            return null;
        }
        if (!Fit.All.Contains(raw.Status) || PythonUnicode.Strip(raw.Reason).Length == 0) {
            return null;
        }
        return new Match(
            raw.Status,
            Clean(raw.Reason),
            Items(raw.Evidence),
            Items(raw.Gaps),
            Items(raw.Blockers),
            raw.IsLowCode,
            raw.RequiredExperienceYears,
            version);
    }

    public static Match? Validate(
        Match match, IReadOnlySet<string> evidenceIds, IReadOnlySet<string> educationIds, Limits limits) {
        if (match.Evidence.Any(item => !evidenceIds.Contains(item))) {
            return null;
        }
        if (match.Blockers.Count > 0 || match.IsLowCode || ExceedsYears(match, limits)) {
            return match with { Status = Fit.Unsuitable };
        }
        if (Fit.Reportable.Contains(match.Status) && match.Evidence.All(educationIds.Contains)) {
            return null;
        }
        if (match.Status == Fit.Strong && match.Gaps.Count > 0) {
            return match with { Status = Fit.Stretch };
        }
        return match;
    }

    private static string YearsText(double value) =>
        value.ToString(shortFormat, CultureInfo.InvariantCulture).Replace(upperExponent, lowerExponent);

    private static string YearCapRule(string years) =>
        $"The search accepts roles requiring at most {years} years. A mandatory minimum above {years} years is unsuitable, "
        + "even with skill fit; name the shortfall as a blocker. A master's, or a bachelor's with relevant work "
        + $"experience, is unsuitable when that experience route needs more than {years} years or does not state the "
        + "years; name it as a blocker.";

    private static bool ExceedsYears(Match match, Limits limits) =>
        match.RequiredExperienceYears is double years && MaxYears(limits) is double cap && years > cap;

    private static List<string> Items(IReadOnlyList<string> items) =>
        [.. items.Where(item => PythonUnicode.Strip(item).Length > 0).Select(Clean).Take(itemLimit)];

    private static string Clean(string text) => WordTruncation.Cut(PythonUnicode.CollapseSpace(text), textLimit);
}
