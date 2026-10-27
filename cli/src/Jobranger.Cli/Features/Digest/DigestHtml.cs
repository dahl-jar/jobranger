using System.Collections.Frozen;
using System.Text;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Digest;

internal static class DigestHtml {
    public const string ProductName = "jobranger";

    private const string wrap = "overflow-wrap:anywhere;word-break:break-word;";
    private const string hyphenate = "-webkit-hyphens:auto;hyphens:auto;";
    private const string adLanguage = "nb";
    private const int dueColumnPx = 100;
    private const int dueColumnGapPx = 20;
    private const string font =
        "'JetBrainsMono Nerd Font', 'JetBrains Mono', ui-monospace, Menlo, Consolas, monospace";
    private const string tableOpen = "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"";
    private const string pageBackground = "#ECE8E1";
    private const string cardBackground = "#F8F6F2";
    private const string cardBorder = "#E2D9C8";
    private const string ink = "#1E1B17";
    private const string muted = "#68625A";
    private const string bodyText = "#46413A";
    private const string rule = "#DED8CE";
    private const string strongColor = "#24774F";
    private const string partialColor = "#92600B";
    private const int headerRuleGap = 12;
    private const int headerSecondRuleGap = 3;
    private const int footerRuleGap = 28;
    private const string lineBreak = "<br>";
    private const char noteSeparator = ' ';

    private static readonly FrozenDictionary<DigestFit, string> fitColors = new Dictionary<DigestFit, string> {
        [DigestFit.Strong] = strongColor,
        [DigestFit.Partial] = partialColor,
        [DigestFit.OnRequest] = muted,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<Language, string> languageCodes = new Dictionary<Language, string> {
        [Language.En] = "en",
        [Language.Nb] = "nb",
    }.ToFrozenDictionary();

    public static string Page(DigestView view, IReadOnlyList<string> cards) {
        StringBuilder body = new(Header(view));
        int next = 0;
        foreach (SectionView section in view.Sections) {
            string sectionCards = string.Concat(cards.Skip(next).Take(section.Cards.Count));
            next += section.Cards.Count;
            body.Append(Section(section, sectionCards, view.DroppedNote));
        }
        body.Append(Hidden(view)).Append(Footer(view));
        return $"<!doctype html><html lang=\"{languageCodes[view.Language]}\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
            + "<meta name=\"color-scheme\" content=\"light only\">"
            + "<meta name=\"supported-color-schemes\" content=\"light only\">"
            + $"<title>{HtmlText.Escape(view.Subject)}</title></head>"
            + $"<body style=\"margin:0;padding:0;background:{pageBackground};\">"
            + $"{tableOpen} width=\"100%\" style=\"background:{pageBackground};\"><tr>"
            + "<td align=\"center\" style=\"padding:36px 3%;\">"
            + $"{tableOpen} width=\"600\" style=\"width:100%;max-width:600px;"
            + $"background:{cardBackground};border:1px solid {cardBorder};"
            + $"font-family:{font};color:{ink};\">"
            + $"<tr><td style=\"padding:36px 6% 32px;\">{body}</td></tr></table>"
            + "</td></tr></table></body></html>";
    }

    public static string Card(CardView card) {
        string links = string.Concat(card.Links.Select(link =>
            $"<a href=\"{HtmlText.Escape(link.Href)}\" style=\"color:{(link.Muted ? muted : ink)};"
            + $"margin-right:18px;display:inline-block;\">{HtmlText.Escape(link.Label)}</a>"));
        string linkRow = links.Length > 0
            ? $"<div style=\"margin-top:16px;font-size:14px;font-weight:500;{wrap}\">{links}</div>"
            : "";
        string why = card.Why.Length > 0
            ? $"<p style=\"margin:14px 0 0;font-size:17px;line-height:1.45;{wrap}\">{HtmlText.Escape(card.Why)}</p>"
            : "";
        return $"{tableOpen} width=\"100%\" "
            + $"style=\"table-layout:fixed;border-bottom:1px solid {rule};\"><tr>"
            + $"<td width=\"{dueColumnPx + dueColumnGapPx}\" valign=\"top\" "
            + $"style=\"width:{dueColumnPx}px;padding:22px 20px 22px 0;{wrap}\">"
            + "<div style=\"font-size:22px;font-weight:600;line-height:1.1;\">"
            + $"{HtmlText.Escape(card.Due)}</div>"
            + $"<div style=\"font-size:12px;color:{muted};margin-top:4px;\">"
            + $"{HtmlText.Escape(card.DueNote)}</div>"
            + "<div style=\"margin-top:14px;font-size:12px;font-weight:600;"
            + $"color:{fitColors[card.Fit]};\">{HtmlText.Escape(card.FitLabel)}</div></td>"
            + $"<td valign=\"top\" style=\"padding:22px 0;{wrap}\">"
            + $"<div lang=\"{adLanguage}\" style=\"font-size:13px;color:{muted};"
            + $"{wrap}{hyphenate}\">{HtmlText.Escape(card.Company)}</div>"
            + $"<div lang=\"{adLanguage}\" style=\"font-size:23px;font-weight:600;"
            + $"line-height:1.2;margin-top:2px;{wrap}{hyphenate}\">{HtmlText.Escape(card.Title)}</div>"
            + $"<div style=\"margin-top:8px;font-size:13px;color:{bodyText};{wrap}\">"
            + $"{HtmlText.Escape(card.Meta)}</div>"
            + $"{why}{linkRow}</td></tr></table>";
    }

    private static string Rule(string color, int gap) =>
        $"<div style=\"height:1px;line-height:1px;font-size:1px;background:{color};"
        + $"margin-top:{gap}px;\">&nbsp;</div>";

    private static string Header(DigestView view) =>
        $"{tableOpen} width=\"100%\"><tr>"
        + "<td style=\"font-size:26px;font-weight:600;letter-spacing:-0.01em;\">"
        + $"{ProductName}</td>"
        + $"<td align=\"right\" style=\"font-size:13px;color:{muted};{wrap}\">"
        + $"{HtmlText.Escape(view.DateLine)}</td></tr></table>"
        + $"{Rule(ink, headerRuleGap)}{Rule(ink, headerSecondRuleGap)}"
        + $"<p style=\"margin:22px 0 0;font-size:22px;line-height:1.4;{wrap}\">"
        + $"{HtmlText.Escape(view.Lead)}</p>";

    private static string Section(SectionView section, string cards, string droppedNote) {
        string label = $"{tableOpen} width=\"100%\" style=\"margin-top:30px;\"><tr>"
            + "<td style=\"white-space:nowrap;padding-right:12px;font-size:12px;"
            + "font-weight:600;letter-spacing:0.12em;text-transform:uppercase;\">"
            + $"{HtmlText.Escape(section.Title)}</td>"
            + $"<td width=\"100%\">{Rule(rule, 0)}</td></tr></table>";
        string dropped = section.Dropped > 0
            ? $"<p style=\"margin:14px 0 0;font-size:13px;color:{muted};\">"
                + $"{section.Dropped} {HtmlText.Escape(droppedNote)}</p>"
            : "";
        return label + cards + dropped;
    }

    private static string Hidden(DigestView view) {
        if (view.HiddenText.Length == 0) {
            return "";
        }
        string link = view.HiddenLink is LinkView hidden
            ? $"{TerminalLines.Separator}<a href=\"{HtmlText.Escape(hidden.Href)}\" "
                + $"style=\"color:{ink};\">{HtmlText.Escape(hidden.Label)}</a>"
            : "";
        return "<p style=\"margin:26px 0 0;font-size:14px;line-height:1.6;"
            + $"color:{bodyText};{wrap}\">"
            + $"{HtmlText.Escape(view.HiddenText)}{link}</p>";
    }

    private static string Footer(DigestView view) {
        string links = string.Join(
            TerminalLines.Separator,
            view.FooterLinks.Select(link =>
                $"<a href=\"{HtmlText.Escape(link.Href)}\" style=\"color:{muted};\">{HtmlText.Escape(link.Label)}</a>"));
        string lastLine = links.Length > 0 ? links : HtmlText.Escape(view.StopLine);
        string notes = HtmlText.Escape(string.Join(noteSeparator, view.Notes));
        string footer = notes.Length > 0 ? notes + lineBreak + lastLine : lastLine;
        return $"{Rule(rule, footerRuleGap)}"
            + "<p style=\"margin:14px 0 0;font-size:12px;line-height:1.7;"
            + $"color:{muted};{wrap}\">"
            + $"{footer}</p>";
    }
}
