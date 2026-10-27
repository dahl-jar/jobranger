using System.Text;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Digest;

internal static class CardText {
    public const int FieldLimit = 200;
    public const int SummaryLimit = 1000;

    private const string ellipsis = "…";
    private const char space = ' ';

    private static readonly CodeRanges bidiControls = new([(0x202A, 0x202E), (0x2066, 0x2069)]);

    public static string Cut(string text, int limit) {
        StringBuilder flat = new(text.Length);
        bool inSpace = false;
        bool inText = false;
        foreach (int code in CodePoints.Of(text)) {
            if (PythonUnicode.IsSpace(code)) {
                inSpace = true;
                continue;
            }
            if (inSpace && inText) {
                flat.Append(space);
            }
            inSpace = false;
            inText = true;
            if (!PythonUnicode.IsHidden(code) && !bidiControls.Contains(code)) {
                CodePoints.Append(flat, code);
            }
        }
        string shown = flat.ToString();
        if (CodePoints.Count(shown) <= limit) {
            return shown;
        }
        return shown[..CodePoints.IndexAfter(shown, limit - 1)] + ellipsis;
    }
}
