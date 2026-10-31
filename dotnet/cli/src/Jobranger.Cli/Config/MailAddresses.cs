using System.Globalization;

namespace Jobranger.Cli.Config;

internal static class MailAddresses {
    private const char at = '@';
    private const char dot = '.';
    private const char quote = '"';
    private const char backslash = '\\';
    private const char openAngle = '<';
    private const char closeAngle = '>';
    private const char space = ' ';
    private const string atomSymbols = "!#$%&'*+-/=?^_`{|}~";
    private const string phraseSymbols = "!#$%&'*+-/=?^_`{|}~. ";

    private static readonly IdnMapping idn = new() { UseStd3AsciiRules = true };

    public static bool IsAddress(string address) {
        int separator = address.IndexOf(at, StringComparison.Ordinal);
        if (separator < 0 || separator != address.LastIndexOf(at)) {
            return false;
        }
        return IsDotAtom(address[..separator]) && AsciiDomain(address[(separator + 1)..]) is not null;
    }

    public static bool IsMailbox(string value) {
        int open = value.IndexOf(openAngle, StringComparison.Ordinal);
        if (open < 0) {
            return IsAddress(value);
        }
        if (value[^1] != closeAngle || value != value.Trim()) {
            return false;
        }
        string display = value[..open].TrimEnd(space);
        return IsPhrase(display) && IsAddress(value[(open + 1)..^1]);
    }

    public static string? AsciiDomain(string domain) {
        if (domain.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))) {
            return null;
        }
        try {
            string ascii = idn.GetAscii(domain);
            return ascii.Split(dot).Any(label => label.Length == 0) ? null : ascii;
        } catch (ArgumentException) {
            return null;
        }
    }

    public static string? AsciiAddress(string address) {
        if (!IsAddress(address)) {
            return null;
        }
        int separator = address.IndexOf(at, StringComparison.Ordinal);
        return $"{address[..(separator + 1)]}{AsciiDomain(address[(separator + 1)..])}";
    }

    public static string? MailboxWithAsciiDomain(string value) {
        if (!IsMailbox(value)) {
            return null;
        }
        int open = value.IndexOf(openAngle, StringComparison.Ordinal);
        return open < 0
            ? AsciiAddress(value)
            : $"{value[..(open + 1)]}{AsciiAddress(value[(open + 1)..^1])}{closeAngle}";
    }

    private static bool IsDotAtom(string text) =>
        text.Split(dot).All(atom => atom.Length > 0 && atom.All(IsAtomCharacter));

    private static bool IsAtomCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || atomSymbols.Contains(character, StringComparison.Ordinal);

    private static bool IsPhrase(string display) {
        if (display.Length >= 2 && display[0] == quote && display[^1] == quote) {
            return display[1..^1].All(character => character is not (quote or backslash) && !char.IsControl(character));
        }
        return display.All(character =>
            char.IsLetterOrDigit(character) || phraseSymbols.Contains(character, StringComparison.Ordinal));
    }
}
