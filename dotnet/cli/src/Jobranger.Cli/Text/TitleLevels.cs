using System.Text;
using System.Text.RegularExpressions;

namespace Jobranger.Cli.Text;

internal static partial class TitleLevels {
    public const string Lead = "lead";
    public const string Senior = "senior";

    private const char otherWord = '_';
    private const char otherSymbol = '!';

    public static string? Of(string title) {
        string folded = Fold(title);
        if (JuniorWords().IsMatch(folded)) {
            return null;
        }
        if (LeadWords().IsMatch(folded)) {
            return Lead;
        }
        return SeniorWords().IsMatch(folded) ? Senior : null;
    }

    private static string Fold(string title) {
        StringBuilder folded = new(title.Length);
        foreach (int code in CodePoints.Of(title)) {
            folded.Append(PythonUnicode.IgnoreCaseLetter(code) ?? (PythonUnicode.IsWord(code) ? otherWord : otherSymbol));
        }
        return folded.ToString();
    }

    [GeneratedRegex(@"\b(?:lead|principal|staff|architect|arkitekt|tech lead|teamleder)\b")]
    private static partial Regex LeadWords();

    [GeneratedRegex(@"\b(?:senior(?:utvikler|ingeniør|rådgiver)?|sr\.?|erfaren|experienced)\b")]
    private static partial Regex SeniorWords();

    [GeneratedRegex(@"\b(?:junior|graduate|nyutdannet|trainee|intern|praktikant|sommerjobb)\b")]
    private static partial Regex JuniorWords();
}
