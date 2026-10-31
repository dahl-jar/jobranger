using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Digest;

internal static class TerminalLines {
    public const string Separator = " · ";

    public static string HiddenLine(IReadOnlyList<HiddenCount> hidden, double? maxYears, IMessages messages) {
        List<HiddenCount> shown = [.. hidden.Where(part => part.Count > 0).OrderByDescending(part => part.Count)];
        if (shown.Count == 0) {
            return "";
        }
        string parts = string.Join(Separator, shown.Select(part => Part(part, maxYears, messages)));
        return messages.HiddenLine(shown.Sum(part => part.Count), parts);
    }

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
