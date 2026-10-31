using System.Text;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Digest;

internal static class DigestRenderer {
    private const int maxBytes = 90_000;
    private const int failedShown = 5;
    private const int noonHour = 12;
    private const int eveningHour = 18;
    private const int overnightEndHour = 6;
    private const string listSeparator = ", ";
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
        CardView[] Cards(IReadOnlyList<DigestCard> group) =>
            [.. group.Select(card => CardViews.Of(card, data.Day, site, messages))];
        CardView[] soon = Cards(data.ApplySoon);
        CardView[] fresh = Cards(data.NewToday);
        CardView[] earlier = Cards(data.Carried);
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
