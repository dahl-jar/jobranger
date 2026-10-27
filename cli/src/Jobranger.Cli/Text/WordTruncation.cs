namespace Jobranger.Cli.Text;

internal static class WordTruncation {
    private const string ellipsis = "\u2026";
    private static readonly char[] trailingPunctuation = [' ', ',', ';', ':', '-'];

    public static string Cut(string text, int limit) {
        if (CodePoints.Count(text) <= limit) {
            return text;
        }
        string head = text[..CodePoints.IndexAfter(text, limit - 1)];
        string shortened = BeforeLastWord(head).TrimEnd(trailingPunctuation);
        return (shortened.Length > 0 ? shortened : head) + ellipsis;
    }

    private static string BeforeLastWord(string text) {
        int end = SkipBack(text, text.Length, space: true);
        int wordStart = SkipBack(text, end, space: false);
        int separatorStart = SkipBack(text, wordStart, space: true);
        return separatorStart == 0 ? text[wordStart..end] : text[..separatorStart];
    }

    private static int SkipBack(string text, int index, bool space) {
        while (index > 0 && PythonUnicode.IsSpace(text[index - 1]) == space) {
            index--;
        }
        return index;
    }
}
