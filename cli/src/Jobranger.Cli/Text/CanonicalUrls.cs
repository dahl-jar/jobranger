namespace Jobranger.Cli.Text;

internal static class CanonicalUrls {
    private const char pathSeparator = '/';
    private const char parameterSeparator = '&';
    private const string trackingPrefix = "utm_";

    public static string Of(string url) {
        SplitUrl parts = PythonUrls.SplitUnchecked(url);
        string path = parts.Path.TrimEnd(pathSeparator);
        string query = Untracked(parts.Query);
        if (parts.Netloc.Length == 0 && path.Length == 0 && query.Length == 0) {
            return string.Empty;
        }
        return PythonUrls.Join(new SplitUrl(parts.Scheme, PythonCase.Lower(parts.Netloc), path, query, string.Empty));
    }

    private static string Untracked(string query) =>
        string.Join(parameterSeparator, query.Split(parameterSeparator)
            .Where(parameter => !parameter.StartsWith(trackingPrefix, StringComparison.OrdinalIgnoreCase)));
}
