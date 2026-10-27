using System.Text;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Digest;

internal static class DigestRenderer {
    private const int maxBytes = 90_000;
    private const int failedShown = 5;
    private const int noonHour = 12;
    private const int eveningHour = 18;
    private const int overnightEndHour = 6;
    private const string arrow = "↗";
    private const string strongGlyph = "✓ ";
    private const string partialGlyph = "◐ ";
    private const string listSeparator = ", ";
    private const string appliedAction = "&action=applied";
    private const string notForMeAction = "&action=not_for_me";
    private const string appPath = "/app";
    private const string hiddenPath = "/app/hidden";
    private const string settingsPath = "/app/settings";
    private const string deliveryPath = "/app/settings/delivery";

    private static readonly DigestFit[] leadFits = [DigestFit.Strong, DigestFit.Partial, DigestFit.OnRequest];

    public static RenderedDigest Render(DigestData data, string? siteUrl, IMessages messages) {
        DigestView view = View(data, DigestUrls.SiteRoot(siteUrl), messages);
        List<string> cards = [.. view.Sections.SelectMany(section => section.Cards).Select(DigestHtml.Card)];
        int kept = KeptCount(DigestHtml.Page(Keep(view, 0), []), cards);
        DigestView shown = Keep(view, kept);
        return new RenderedDigest(
            shown.Subject,
            DigestHtml.Page(shown, cards[..kept]),
            DigestPlainText.Write(shown),
            [.. shown.Sections.SelectMany(section => section.Cards).Select(card => card.Id)]);
    }

    private static DigestView View(DigestData data, string? site, IMessages messages) {
        CardView[] soon = [.. data.ApplySoon.Select(card => Card(card, data.Day, site, messages))];
        CardView[] fresh = [.. data.NewToday.Select(card => Card(card, data.Day, site, messages))];
        CardView[] earlier = [.. data.Carried.Select(card => Card(card, data.Day, site, messages))];
        SectionView[] sections = [
            .. new[] {
                new SectionView(messages.DigestApplySoonTitle, soon, 0),
                new SectionView(messages.DigestNewTodayTitle, fresh, 0),
                new SectionView(messages.DigestCarriedTitle, earlier, 0),
            }.Where(section => section.Cards.Count > 0),
        ];
        string hiddenText = TerminalLines.HiddenLine(data.Hidden, data.MaxYears, messages);
        string dateLine = messages.DigestLongDate(data.Day);
        return new DigestView(
            messages.Language,
            messages.DigestSubject(soon.Length + fresh.Length + earlier.Length, dateLine),
            dateLine,
            Lead([.. soon, .. fresh], data.NewAds, data.RanAt, messages),
            sections,
            hiddenText,
            site is not null && hiddenText.Length > 0 ? new LinkView(messages.DigestViewHidden, site + hiddenPath) : null,
            Notes(data, messages),
            FooterLinks(site, messages),
            messages.DigestStopEmails,
            site is not null ? messages.DigestMoreOnWebsite : messages.DigestMoreInReport);
    }

    private static CardView Card(DigestCard card, DateOnly day, string? site, IMessages messages) {
        DateOnly? deadline = card.DeadlineDate;
        string[] meta = [card.Location, card.RoleLabel, DigestSources.Label(card.Source, messages)];
        return new CardView(
            card.Id,
            deadline is DateOnly due ? messages.DigestShortDate(due) : messages.DigestNoDate,
            messages.DigestDueNote(deadline?.DayNumber - day.DayNumber),
            card.Fit,
            FitLabel(card.Fit, messages),
            CardText.Cut(card.Company, CardText.FieldLimit),
            CardText.Cut(string.IsNullOrEmpty(card.DisplayTitle) ? card.Title : card.DisplayTitle, CardText.FieldLimit),
            string.Join(
                TerminalLines.Separator,
                meta.Where(part => PythonUrls.Strip(part).Length > 0).Select(part => CardText.Cut(part, CardText.FieldLimit))),
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

    private static LinkView[] FooterLinks(string? site, IMessages messages) {
        if (site is null) {
            return [];
        }
        return [
            new LinkView(messages.DigestOpenApp, site + appPath),
            new LinkView(messages.DigestSettings, site + settingsPath),
            new LinkView(messages.DigestUnsubscribe, site + deliveryPath),
        ];
    }

    private static string Lead(IReadOnlyList<CardView> cards, int newAds, DateTimeOffset ranAt, IMessages messages) {
        string when = PartOfDay(ranAt, messages);
        string[] shown = [
            .. leadFits
                .Select(fit => (Fit: fit, Count: cards.Count(card => card.Fit == fit)))
                .Where(group => group.Count > 0)
                .Select(group => FitPhrase(group.Fit, group.Count, messages)),
        ];
        return shown.Length == 0
            ? messages.DigestNoNewMatches(when, newAds)
            : messages.DigestLead(JoinAnd(shown, messages), when, newAds);
    }

    private static string FitPhrase(DigestFit fit, int count, IMessages messages) => fit switch {
        DigestFit.Strong => messages.DigestStrongFits(count),
        DigestFit.Partial => messages.DigestPartialFits(count),
        _ => messages.DigestShownOnRequest(count),
    };

    private static string PartOfDay(DateTimeOffset ranAt, IMessages messages) {
        if (ranAt.Hour < noonHour) {
            return messages.DigestThisMorning;
        }
        return ranAt.Hour < eveningHour ? messages.DigestThisAfternoon : messages.DigestThisEvening;
    }

    private static string[] Notes(DigestData data, IMessages messages) {
        string[] notes = [
            ReadNote(data, messages),
            data.ClosedSinceLast > 0 ? messages.DigestClosedNote(data.ClosedSinceLast) : "",
            FailedNote(data.FailedSources, messages),
            data.BudgetPaused ? messages.DigestPausedNote(data.Pending) : "",
            StopNote(data, messages),
        ];
        return [.. notes.Where(note => note.Length > 0)];
    }

    private static string ReadNote(DigestData data, IMessages messages) {
        List<string> read = [
            .. data.SourcesRead
                .Where(source => !DigestSources.IsDirect(source))
                .Select(source => DigestSources.Phrase(source, messages)),
        ];
        if (data.EmployerPages > 0) {
            read.Add(messages.DigestCareerPages(data.EmployerPages));
        }
        if (read.Count == 0) {
            return "";
        }
        return messages.DigestReadNote(JoinAnd(read, messages), data.RanAt.Hour < overnightEndHour);
    }

    private static string FailedNote(IReadOnlyList<string> failed, IMessages messages) {
        if (failed.Count == 0) {
            return "";
        }
        string[] names = [.. failed.Select(source => DigestSources.Phrase(source, messages))];
        string joined = names.Length <= failedShown
            ? JoinAnd(names, messages)
            : messages.DigestJoin(
                string.Join(listSeparator, names.Take(failedShown)), messages.DigestMoreSources(names.Length - failedShown));
        return messages.DigestCouldNotRead(joined);
    }

    private static string StopNote(DigestData data, IMessages messages) => data.ModelStopped switch {
        DigestStop.Auth => messages.DigestKeyRefusedNote(data.Pending),
        DigestStop.ProviderDown => messages.DigestProviderDownNote(data.Pending),
        _ => "",
    };

    private static string JoinAnd(IReadOnlyList<string> items, IMessages messages) {
        if (items.Count <= 1) {
            return string.Concat(items);
        }
        return messages.DigestJoin(string.Join(listSeparator, items.Take(items.Count - 1)), items[^1]);
    }

    private static int KeptCount(string frame, List<string> cards) {
        int total = Encoding.UTF8.GetByteCount(frame);
        for (int kept = 0; kept < cards.Count; kept++) {
            total += Encoding.UTF8.GetByteCount(cards[kept]);
            if (total > maxBytes) {
                return kept;
            }
        }
        return cards.Count;
    }

    private static DigestView Keep(DigestView view, int count) {
        List<SectionView> sections = [];
        int left = count;
        foreach (SectionView section in view.Sections) {
            CardView[] cards = [.. section.Cards.Take(left)];
            left -= cards.Length;
            sections.Add(section with { Cards = cards, Dropped = section.Cards.Count - cards.Length });
        }
        return view with { Sections = sections };
    }
}
