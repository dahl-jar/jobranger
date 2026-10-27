using System.Globalization;
using Jobranger.Cli.Config;
using Jobranger.Cli.Text;
using RoleCatalogue = Jobranger.Cli.Catalogue.Catalogue;

namespace Jobranger.Cli.Features.Matching;

internal static class AdRules {
    private const string isoDateFormat = "yyyy-MM-dd";
    private const int isoDateLength = 10;
    private const int staleAgeDays = 90;

    public static bool HasCurrentMatch(AdView ad, string version) => ad.Match?.Version == version;

    public static HiddenKind? HiddenReason(AdView ad, string version, Targets targets) =>
        AreaReason(ad, targets) ?? LimitReason(ad, targets.Limits) ?? PlaceReason(ad, targets) ?? FitReason(ad, version);

    public static bool IsStale(AdView ad, DateOnly today) =>
        ParseDate(ad.Published) is DateOnly published && today.DayNumber - published.DayNumber > staleAgeDays;

    public static DateOnly? ParseDate(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return null;
        }
        string head = value.Length > isoDateLength ? value[..isoDateLength] : value;
        return DateOnly.TryParseExact(
            head, isoDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date
            : null;
    }

    public static DisplayFit FitOf(AdView ad, Targets targets) {
        if (ad.Match is Match match && !Fit.Reportable.Contains(match.Status)) {
            return DisplayFit.OnRequest;
        }
        if (ad.Match?.Status != Fit.Strong || !PlaceMatches(ad.Location, targets.Places)) {
            return DisplayFit.Partial;
        }
        return DisplayFit.Strong;
    }

    private static bool PlaceMatches(string location, IReadOnlyList<string> places) {
        string normalized = Normalize(location);
        if (places.Count == 0 || normalized.Length == 0) {
            return true;
        }
        return places.Any(place => ContainsWord(normalized, Normalize(place)));
    }

    private static HiddenKind? AreaReason(AdView ad, Targets targets) {
        if (ad.IsProgrammingJob != true) {
            return HiddenKind.Other;
        }
        if (ad.RoleType != RoleCatalogue.GeneralSoftware && !targets.Subfields.Contains(ad.RoleType)) {
            return HiddenKind.Role;
        }
        return null;
    }

    private static HiddenKind? LimitReason(AdView ad, Limits limits) {
        if (RoleCatalogue.LevelsFromLabel.GetValueOrDefault(ad.Label ?? string.Empty) is string level
            && !limits.Levels.Contains(level)) {
            return HiddenKind.Senior;
        }
        if (ad.Match?.RequiredExperienceYears is double years && !limits.Years.Contains(MatchRules.BandOf(years))) {
            return HiddenKind.Years;
        }
        if (limits.HideMasters && ad.RequiresMaster) {
            return HiddenKind.Masters;
        }
        return null;
    }

    private static HiddenKind? PlaceReason(AdView ad, Targets targets) {
        if (targets.Places.Count > 0 && !targets.Relocate && !PlaceMatches(ad.Location, targets.Places)) {
            return HiddenKind.Place;
        }
        return null;
    }

    private static HiddenKind? FitReason(AdView ad, string version) {
        if (ad.Match is not Match match || match.Version != version || !Fit.Reportable.Contains(match.Status)
            || match.Blockers.Count > 0 || match.IsLowCode) {
            return HiddenKind.Other;
        }
        return null;
    }

    private static string Normalize(string value) => MatchText.CollapseSpace(PythonUnicode.CaseFold(value));

    private static bool ContainsWord(string text, string word) {
        int start = text.IndexOf(word, StringComparison.Ordinal);
        while (start >= 0) {
            if (IsBoundary(text, start) && IsBoundary(text, start + word.Length)) {
                return true;
            }
            start = start < text.Length ? text.IndexOf(word, start + 1, StringComparison.Ordinal) : -1;
        }
        return false;
    }

    private static bool IsBoundary(string text, int index) =>
        IsWordAt(text, index - 1) != IsWordAt(text, index);

    private static bool IsWordAt(string text, int index) =>
        index >= 0 && index < text.Length && PythonUnicode.IsWord(text[index]);
}
