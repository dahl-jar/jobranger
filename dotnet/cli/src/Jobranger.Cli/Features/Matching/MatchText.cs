using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Matching;

internal static class MatchText {
    private const char space = ' ';

    public static string Strip(string text) {
        int start = 0;
        int end = text.Length;
        while (start < end && PythonUnicode.IsSpace(text[start])) {
            start++;
        }
        while (end > start && PythonUnicode.IsSpace(text[end - 1])) {
            end--;
        }
        return text[start..end];
    }

    public static string CollapseSpace(string text) => string.Join(space, Words(text));

    public static string Head(string text, int limit) => text[..CodePoints.IndexAfter(text, limit)];

    private static List<string> Words(string text) {
        List<string> words = [];
        int index = 0;
        while (index < text.Length) {
            while (index < text.Length && PythonUnicode.IsSpace(text[index])) {
                index++;
            }
            int start = index;
            while (index < text.Length && !PythonUnicode.IsSpace(text[index])) {
                index++;
            }
            if (index > start) {
                words.Add(text[start..index]);
            }
        }
        return words;
    }
}
