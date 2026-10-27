namespace Jobranger.Cli.Text;

internal static class CanonicalUrls {
    private const char space = ' ';
    private const char schemeEnd = ':';
    private const string netlocStart = "//";
    private const char fragmentStart = '#';
    private const char queryStart = '?';
    private const char pathSeparator = '/';
    private const string schemeCharacters = "+-.";
    private const char parameterSeparator = '&';
    private const string trackingPrefix = "utm_";

    private static readonly string[] unsafeUrlCharacters = ["\t", "\r", "\n"];
    private static readonly char[] netlocEnds = ['/', '?', '#'];
    private static readonly char[] controlOrSpace = [.. Enumerable.Range(0, space + 1).Select(code => (char)code)];

    public static string Of(string url) {
        string rest = url.TrimStart(controlOrSpace);
        foreach (string removed in unsafeUrlCharacters) {
            rest = rest.Replace(removed, string.Empty, StringComparison.Ordinal);
        }
        string scheme = string.Empty;
        int colon = rest.IndexOf(schemeEnd);
        if (colon > 0 && char.IsAsciiLetter(rest[0]) && rest[..colon].All(IsSchemeCharacter)) {
            scheme = rest[..colon].ToLowerInvariant();
            rest = rest[(colon + 1)..];
        }
        string netloc = string.Empty;
        if (rest.StartsWith(netlocStart, StringComparison.Ordinal)) {
            int end = rest.IndexOfAny(netlocEnds, netlocStart.Length);
            end = end < 0 ? rest.Length : end;
            netloc = rest[netlocStart.Length..end];
            rest = rest[end..];
        }
        string[] pathAndQuery = rest.Split(fragmentStart)[0].Split(queryStart, 2);
        string path = pathAndQuery[0].TrimEnd(pathSeparator);
        string query = pathAndQuery.Length > 1 ? Untracked(pathAndQuery[1]) : string.Empty;
        if (netloc.Length == 0 && path.Length == 0 && query.Length == 0) {
            return string.Empty;
        }
        string canonical = scheme + schemeEnd + netlocStart + PythonCase.Lower(netloc) + path;
        return query.Length > 0 ? canonical + queryStart + query : canonical;
    }

    private static string Untracked(string query) =>
        string.Join(parameterSeparator, query.Split(parameterSeparator)
            .Where(parameter => !parameter.StartsWith(trackingPrefix, StringComparison.OrdinalIgnoreCase)));

    private static bool IsSchemeCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || schemeCharacters.Contains(character);
}
