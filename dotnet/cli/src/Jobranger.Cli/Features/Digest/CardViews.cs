using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Digest;

internal static class CardViews {
    private const string arrow = "↗";
    private const string strongGlyph = "✓ ";
    private const string partialGlyph = "◐ ";
    private const string appliedAction = "&action=applied";
    private const string notForMeAction = "&action=not_for_me";

    public static CardView Of(DigestCard card, DateOnly day, string? site, IMessages messages) {
        DateOnly? deadline = card.DeadlineDate;
        string[] meta = [card.Location, card.RoleLabel, DigestSources.Label(card.Source, messages)];
        return new CardView(
            card.Id,
            deadline is DateOnly due ? messages.DigestShortDate(due) : messages.DigestNoDate,
            messages.DigestDueNote(deadline?.DayNumber - day.DayNumber),
            card.Fit,
            FitLabel(card.Fit, messages),
            CardText.Cut(card.Company, CardText.FieldLimit),
            CardText.Cut(card.ShownTitle, CardText.FieldLimit),
            string.Join(
                TerminalLines.Separator,
                meta.Where(part => PythonUnicode.Strip(part).Length > 0)
                    .Select(part => CardText.Cut(part, CardText.FieldLimit))),
            CardText.Cut(card.Summary ?? "", CardText.SummaryLimit),
            Links(card, site, messages));
    }

    private static string FitLabel(DigestFit fit, IMessages messages) => fit switch {
        DigestFit.Strong => strongGlyph + messages.DigestStrongLabel,
        DigestFit.Partial => partialGlyph + messages.DigestPartialLabel,
        _ => messages.DigestOnRequestLabel,
    };

    private static List<LinkView> Links(DigestCard card, string? site, IMessages messages) {
        List<LinkView> links = [];
        if (DigestUrls.Web(card.Url) is WebUrl web) {
            links.Add(new LinkView($"{messages.DigestOpenAd} {arrow}", web.Text));
        }
        if (site is not null && DigestUrls.AdLink(site, card.Id) is string adLink) {
            links.Add(new LinkView(messages.DigestMarkApplied, adLink + appliedAction));
            links.Add(new LinkView(messages.DigestNotForMe, adLink + notForMeAction, Muted: true));
        }
        return links;
    }
}
