using System.Text;

namespace Jobranger.Cli.Platform;

internal sealed record EditorCommand(string Program, IReadOnlyList<string> Arguments) {
    private const char singleQuote = '\'';
    private const char doubleQuote = '"';

    public static EditorCommand? Parse(string value) {
        List<string> words = Split(value);
        if (words.Count == 0) {
            return null;
        }
        return words[0].Length > 0
            ? new EditorCommand(words[0], words[1..])
            : throw new EditorException(EditorProblem.NotStarted, value);
    }

    private static List<string> Split(string value) {
        List<string> words = [];
        StringBuilder word = new();
        bool inWord = false;
        char? quote = null;
        foreach (char letter in value) {
            if (quote is not null) {
                if (letter == quote) {
                    quote = null;
                } else {
                    word.Append(letter);
                }
            } else if (letter is singleQuote or doubleQuote) {
                quote = letter;
                inWord = true;
            } else if (!char.IsWhiteSpace(letter)) {
                word.Append(letter);
                inWord = true;
            } else if (inWord) {
                words.Add(word.ToString());
                word.Clear();
                inWord = false;
            }
        }
        if (quote is not null) {
            throw new EditorException(EditorProblem.NotStarted, value);
        }
        if (inWord) {
            words.Add(word.ToString());
        }
        return words;
    }
}
