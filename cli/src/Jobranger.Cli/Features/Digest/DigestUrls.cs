using System.Collections.Frozen;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Digest;

internal static class DigestUrls {
    private const int urlLimit = 2048;
    private const string urlSafe = "/?:@!$&'()*+,;=%~";
    private const char userSeparator = '@';
    private const char pathSeparator = '/';
    private const string adPath = "/app/ad?id=";
    private const string idSafe = "";

    private static readonly FrozenSet<string> webSchemes = FrozenSet.Create(StringComparer.Ordinal, "http", "https");

    public static WebUrl? Web(string url) {
        if (CodePoints.Count(url) > urlLimit) {
            return null;
        }
        SplitUrl? parts = PythonUrls.Split(PythonUrls.Strip(PythonUrls.ReplaceSurrogates(url)));
        if (parts is null || !webSchemes.Contains(parts.Scheme) || parts.Hostname is null
            || parts.Netloc.Contains(userSeparator)) {
            return null;
        }
        if (CodePoints.Of(parts.Netloc).Any(code => PythonUnicode.IsSpace(code) || !PythonUnicode.IsPrintable(code))) {
            return null;
        }
        SplitUrl quoted = parts with {
            Path = PythonUrls.Quote(parts.Path, urlSafe),
            Query = PythonUrls.Quote(parts.Query, urlSafe),
            Fragment = PythonUrls.Quote(parts.Fragment, urlSafe),
        };
        string text = PythonUrls.Join(quoted);
        return CodePoints.Count(text) > urlLimit ? null : new WebUrl(quoted, text);
    }

    public static string? SiteRoot(string? siteUrl) {
        if (string.IsNullOrEmpty(siteUrl) || Web(siteUrl) is not WebUrl web) {
            return null;
        }
        return PythonUrls.Join(web.Parts with { Path = web.Parts.Path.TrimEnd(pathSeparator), Query = "", Fragment = "" });
    }

    public static string? AdLink(string siteRoot, string adId) {
        string link = siteRoot + adPath + PythonUrls.Quote(PythonUrls.ReplaceSurrogates(adId), idSafe);
        return CodePoints.Count(link) > urlLimit ? null : link;
    }
}
