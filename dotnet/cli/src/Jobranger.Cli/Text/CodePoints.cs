using System.Text;

namespace Jobranger.Cli.Text;

internal static class CodePoints {
    public static IEnumerable<int> Of(string text) {
        int index = 0;
        while (index < text.Length) {
            int code = At(text, index);
            yield return code;
            index += Width(code);
        }
    }

    public static int Count(string text) => Of(text).Count();

    public static int IndexAfter(string text, int count) {
        int index = 0;
        for (int seen = 0; seen < count && index < text.Length; seen++) {
            index += Width(At(text, index));
        }
        return index;
    }

    public static void Append(StringBuilder builder, int code) {
        if (Width(code) == 1) {
            builder.Append((char)code);
        } else {
            builder.Append(char.ConvertFromUtf32(code));
        }
    }

    private static int At(string text, int index) =>
        char.IsSurrogatePair(text, index) ? char.ConvertToUtf32(text[index], text[index + 1]) : text[index];

    private static int Width(int code) => code > char.MaxValue ? 2 : 1;
}
