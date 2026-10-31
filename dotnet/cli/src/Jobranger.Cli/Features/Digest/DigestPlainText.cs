namespace Jobranger.Cli.Features.Digest;

internal static class DigestPlainText {
    private const string blockSeparator = "\n\n";
    private const string lineSeparator = "\n";
    private const string labelSeparator = ": ";
    private const string dueSeparator = ", ";

    public static string Write(DigestView view) {
        List<string> blocks = [DigestHtml.ProductName + TerminalLines.Separator + view.DateLine, view.Lead];
        foreach (SectionView section in view.Sections) {
            blocks.Add(section.Title.ToUpperInvariant());
            blocks.AddRange(section.Cards.Select(Card));
            if (section.Dropped > 0) {
                blocks.Add($"{section.Dropped} {view.DroppedNote}");
            }
        }
        if (view.HiddenText.Length > 0) {
            string link = view.HiddenLink is LinkView hidden ? TerminalLines.Separator + Link(hidden) : "";
            blocks.Add(view.HiddenText + link);
        }
        List<string> footer = [.. view.Notes, .. view.FooterLinks.Select(Link)];
        if (view.FooterLinks.Count == 0) {
            footer.Add(view.StopLine);
        }
        blocks.Add(string.Join(lineSeparator, footer));
        return string.Join(blockSeparator, blocks) + lineSeparator;
    }

    private static string Card(CardView card) {
        List<string> lines = [
            card.Due + dueSeparator + card.DueNote + TerminalLines.Separator + card.FitLabel,
            card.Company,
            card.Title,
            card.Meta,
        ];
        if (card.Why.Length > 0) {
            lines.Add(card.Why);
        }
        lines.AddRange(card.Links.Select(Link));
        return string.Join(lineSeparator, lines);
    }

    private static string Link(LinkView link) => link.Label + labelSeparator + link.Href;
}
