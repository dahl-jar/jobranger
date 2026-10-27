using System.Text;

namespace Jobranger.Cli.Text;

internal static class HtmlText {
    private const char tagOpen = '<';
    private const char tagClose = '>';
    private const char space = ' ';

    private static readonly (string Character, string Entity)[] escapes = [
        ("&", "&amp;"),
        ("<", "&lt;"),
        (">", "&gt;"),
        ("\"", "&quot;"),
        ("'", "&#x27;"),
    ];

    public static string Strip(string html) => CollapseSpace(HtmlEntities.Decode(ReplaceTags(html)));

    public static string Escape(string text) =>
        escapes.Aggregate(text, (escaped, pair) => escaped.Replace(pair.Character, pair.Entity, StringComparison.Ordinal));

    private static string ReplaceTags(string html) {
        StringBuilder text = new(html.Length);
        int index = 0;
        while (index < html.Length) {
            int open = html.IndexOf(tagOpen, index);
            int close = open < 0 ? -1 : html.IndexOf(tagClose, open + 1);
            if (close < 0) {
                text.Append(html, index, html.Length - index);
                break;
            }
            if (close == open + 1) {
                text.Append(html, index, close - index);
                index = close;
                continue;
            }
            text.Append(html, index, open - index).Append(space);
            index = close + 1;
        }
        return text.ToString();
    }

    private static string CollapseSpace(string text) {
        StringBuilder collapsed = new(text.Length);
        bool inSpace = false;
        foreach (char character in text) {
            if (PythonUnicode.IsSpace(character)) {
                inSpace = true;
                continue;
            }
            if (inSpace) {
                collapsed.Append(space);
                inSpace = false;
            }
            collapsed.Append(character);
        }
        return collapsed.ToString().TrimStart(space);
    }
}
