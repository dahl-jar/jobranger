using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Jobranger.Cli.Text;

internal static partial class PythonUrls {
    private const char lastControlOrSpace = ' ';
    private const char schemeEnd = ':';
    private const string netlocStart = "//";
    private const char fragmentStart = '#';
    private const char queryStart = '?';
    private const char bracketOpen = '[';
    private const char bracketClose = ']';
    private const char futureHostStart = 'v';
    private const char zoneStart = '%';
    private const string schemeSeparator = "://";
    private const string alwaysSafe = "_.-~";
    private const string bracketedHostCharacters = ":.";
    private const char surrogateReplacement = '?';
    private const string schemeSymbols = "+-.";
    private const char percent = '%';
    private const string hexFormat = "X2";

    private static readonly char[] netlocEnds = ['/', '?', '#'];
    private static readonly char[] removedCharacters = ['\t', '\r', '\n'];
    private static readonly char[] controlsAndSpace =
        [.. Enumerable.Range(0, lastControlOrSpace + 1).Select(code => (char)code)];

    public static SplitUrl? Split(string url) {
        string rest = RemoveUnsafe(url.TrimStart(controlsAndSpace));
        string scheme = "";
        int colon = rest.IndexOf(schemeEnd);
        if (colon > 0 && char.IsAsciiLetter(rest[0]) && rest[..colon].All(IsSchemeCharacter)) {
            scheme = rest[..colon].ToLowerInvariant();
            rest = rest[(colon + 1)..];
        }
        string netloc = "";
        if (rest.StartsWith(netlocStart, StringComparison.Ordinal)) {
            int end = rest.IndexOfAny(netlocEnds, netlocStart.Length);
            int stop = end < 0 ? rest.Length : end;
            netloc = rest[netlocStart.Length..stop];
            rest = rest[stop..];
            if (!BracketsAreValid(netloc)) {
                return null;
            }
        }
        string fragment = "";
        int hash = rest.IndexOf(fragmentStart);
        if (hash >= 0) {
            fragment = rest[(hash + 1)..];
            rest = rest[..hash];
        }
        string query = "";
        int question = rest.IndexOf(queryStart);
        if (question >= 0) {
            query = rest[(question + 1)..];
            rest = rest[..question];
        }
        if (CodePoints.Of(netloc).Any(PythonUnicode.ExpandsToUrlDelimiter)) {
            return null;
        }
        return new SplitUrl(scheme, netloc, rest, query, fragment);
    }

    public static string Join(SplitUrl parts) {
        StringBuilder url = new(parts.Scheme);
        url.Append(schemeSeparator).Append(parts.Netloc).Append(parts.Path);
        if (parts.Query.Length > 0) {
            url.Append(queryStart).Append(parts.Query);
        }
        if (parts.Fragment.Length > 0) {
            url.Append(fragmentStart).Append(parts.Fragment);
        }
        return url.ToString();
    }

    public static string Quote(string text, string safe) {
        StringBuilder quoted = new(text.Length);
        foreach (byte value in Encoding.UTF8.GetBytes(text)) {
            char character = (char)value;
            if (char.IsAsciiLetterOrDigit(character) || alwaysSafe.Contains(character) || safe.Contains(character)) {
                quoted.Append(character);
            } else {
                quoted.Append(percent).Append(value.ToString(hexFormat, CultureInfo.InvariantCulture));
            }
        }
        return quoted.ToString();
    }

    public static string ReplaceSurrogates(string text) {
        StringBuilder replaced = new(text.Length);
        for (int index = 0; index < text.Length; index++) {
            if (char.IsSurrogatePair(text, index)) {
                replaced.Append(text, index, 2);
                index++;
            } else if (char.IsSurrogate(text[index])) {
                replaced.Append(surrogateReplacement);
            } else {
                replaced.Append(text[index]);
            }
        }
        return replaced.ToString();
    }

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

    private static string RemoveUnsafe(string url) =>
        string.Concat(url.Where(character => !removedCharacters.Contains(character)));

    private static bool IsSchemeCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || schemeSymbols.Contains(character);

    private static bool BracketsAreValid(string netloc) {
        int open = netloc.IndexOf(bracketOpen);
        bool hasClose = netloc.Contains(bracketClose);
        if (open < 0 || !hasClose) {
            return open < 0 && !hasClose;
        }
        string bracketed = netloc[(open + 1)..];
        int close = bracketed.IndexOf(bracketClose);
        return BracketedHostIsValid(close < 0 ? bracketed : bracketed[..close]);
    }

    private static bool BracketedHostIsValid(string host) {
        if (host.StartsWith(futureHostStart)) {
            return FutureHost().IsMatch(host);
        }
        int zone = host.IndexOf(zoneStart);
        string address = zone < 0 ? host : host[..zone];
        return address.All(character => char.IsAsciiHexDigit(character) || bracketedHostCharacters.Contains(character))
            && IPAddress.TryParse(host, out IPAddress? parsed)
            && parsed.AddressFamily == AddressFamily.InterNetworkV6;
    }

    [GeneratedRegex(@"\Av[a-fA-F0-9]+\..+\z")]
    private static partial Regex FutureHost();
}
