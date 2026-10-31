using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Digest;

internal static class TerminalLines {
    public const string Separator = " · ";

    private const string matchIndent = "  ";
    private const string columnGap = " ";
    private const int fitWidth = 13;
    private const int titleWidth = 22;
    private const int companyWidth = 17;
    private const int locationWidth = 11;

    public static string HiddenLine(IReadOnlyList<HiddenCount> hidden, double? maxYears, IMessages messages) {
        List<HiddenCount> shown = [.. hidden.Where(part => part.Count > 0).OrderByDescending(part => part.Count)];
        if (shown.Count == 0) {
            return "";
        }
        string parts = string.Join(Separator, shown.Select(part => Part(part, maxYears, messages)));
        return messages.HiddenLine(shown.Sum(part => part.Count), parts);
    }

    public static string MatchLine(DigestCard card, IMessages messages) {
        string title = string.IsNullOrEmpty(card.DisplayTitle) ? card.Title : card.DisplayTitle;
        string date = card.DeadlineDate is DateOnly due ? messages.DigestShortDate(due) : messages.ReportNoDate;
        return matchIndent
            + Column(FitLabel(card.Fit, messages), fitWidth)
            + Column(CardText.Cut(title, titleWidth), titleWidth)
            + Column(CardText.Cut(card.Company, companyWidth), companyWidth)
            + Column(CardText.Cut(card.Location, locationWidth), locationWidth)
            + date;
    }

    private static string Column(string text, int width) => text.PadRight(width) + columnGap;

    private static string FitLabel(DigestFit fit, IMessages messages) => fit switch {
        DigestFit.Strong => messages.DigestStrongLabel,
        DigestFit.Partial => messages.DigestPartialLabel,
        _ => messages.DigestOnRequestLabel,
    };

    private static string Part(HiddenCount part, double? maxYears, IMessages messages) => part.Group switch {
        HiddenGroup.Senior => messages.HiddenSenior(part.Count),
        HiddenGroup.Years when maxYears is double cap =>
            messages.HiddenYearsAbove(part.Count, (int)Math.Round(cap, MidpointRounding.ToEven)),
        HiddenGroup.Years => messages.HiddenYearsUnknown(part.Count),
        HiddenGroup.Masters => messages.HiddenMasters(part.Count),
        HiddenGroup.Role => messages.HiddenRole(part.Count),
        HiddenGroup.Place => messages.HiddenPlace(part.Count),
        _ => messages.HiddenOther(part.Count),
    };
}
