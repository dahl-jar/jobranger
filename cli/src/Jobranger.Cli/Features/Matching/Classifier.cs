using System.Text.RegularExpressions;
using Jobranger.Cli.Config;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Matching;

internal static partial class Classifier {
    private const int summaryLimit = 700;
    private const int displayTitleLimit = 80;
    private const int technologyTextLimit = 60;
    private const int requirementTextLimit = 160;
    private const int technologyLimit = 5;
    private const int requirementLimit = 3;
    private const int checksLimit = 5;
    private const int checkTextLimit = 160;
    private const string curlyApostrophe = "’";
    private const string apostrophe = "'";

    public static Classification? ToClassification(
        AdAssessment assessment,
        AdView ad,
        IReadOnlySet<string> evidenceIds,
        IReadOnlySet<string> educationIds,
        Limits limits,
        string version) {
        Match? parsed = MatchRules.Parse(assessment.Match, version);
        Match? match = parsed is null ? null : MatchRules.Validate(parsed, evidenceIds, educationIds, limits);
        if (match is null || RoleFields(assessment) is not { } roleFields) {
            return null;
        }
        string head = MatchText.Head(assessment.Summary, summaryLimit);
        string? summary = head.Length > 0 ? head : null;
        bool requiresMaster = assessment.RequiresMaster || SummaryRequiresMaster(summary);
        if (requiresMaster) {
            match = match with { Status = Fit.Unsuitable };
        }
        Classification result = new(
            assessment.Label,
            summary,
            requiresMaster,
            assessment.IsProgrammingJob,
            roleFields.Title,
            roleFields.Role,
            Items(assessment.Technologies, technologyLimit, technologyTextLimit),
            Items(assessment.Requirements, requirementLimit, requirementTextLimit),
            match,
            Checks(assessment.Checks));
        return WithTitleLevel(ad.Title, result);
    }

    public static Classification WithTitleLevel(string title, Classification result) =>
        TitleLevels.Of(title) is string level ? result with { Label = level } : result;

    private static (string? Title, string? Role)? RoleFields(AdAssessment assessment) {
        if (!assessment.IsProgrammingJob) {
            return (null, null);
        }
        string title = PythonUnicode.Strip(assessment.DisplayTitle ?? string.Empty);
        if (title.Length == 0 || assessment.RoleType is null) {
            return null;
        }
        return (MatchText.Head(title, displayTitleLimit), assessment.RoleType);
    }

    private static List<Check> Checks(IReadOnlyList<Check> checks) => [
        .. checks.Take(checksLimit)
            .Where(check => PythonUnicode.Strip(check.Text).Length > 0)
            .Select(check => check with { Text = WordTruncation.Cut(PythonUnicode.Strip(check.Text), checkTextLimit) }),
    ];

    private static List<string> Items(IReadOnlyList<string> values, int itemLimit, int textLimit) => [
        .. values.Select(PythonUnicode.Strip).Where(value => value.Length > 0)
            .Select(value => WordTruncation.Cut(value, textLimit)).Take(itemLimit),
    ];

    private static bool SummaryRequiresMaster(string? summary) {
        if (summary is null) {
            return false;
        }
        string normalized = summary.Replace(curlyApostrophe, apostrophe, StringComparison.Ordinal);
        return SentenceSplit().Split(normalized).Any(sentence =>
            !MasterAlternative().IsMatch(sentence)
            && (RequiresMasterDegree().IsMatch(sentence) || RelevantMasterDegree().IsMatch(sentence)));
    }

    [GeneratedRegex(@"\b(?:requires?|requiring|must have)\b[^.]{0,100}\bmaster'?s? degree", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RequiresMasterDegree();

    [GeneratedRegex(@"\bwith (?:a )?relevant master'?s? degree", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RelevantMasterDegree();

    [GeneratedRegex(
        @"\bbachelor|\bundergraduate|\bequivalent|\bpreferred|\bdesirable|\boptional|\bnot (?:required|mandatory|necessary)|\bnot require|n't require",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MasterAlternative();

    [GeneratedRegex("[.!?;]")]
    private static partial Regex SentenceSplit();
}
