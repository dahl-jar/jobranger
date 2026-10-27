using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace Jobranger.Cli.Http;

internal static partial class OllamaUrls {
    public const string ApiPath = "/v1";
    public const string TagsPath = "/api/tags";

    private const char slash = '/';
    private const char userSeparator = ':';
    private const string basicScheme = "Basic";
    private const string schemePart = "${scheme}";

    private static readonly string[] schemes = [Uri.UriSchemeHttp, Uri.UriSchemeHttps];

    public static string Root(string url) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)) {
            return url;
        }
        string path = parsed.AbsolutePath.TrimEnd(slash);
        while (path.EndsWith(ApiPath, StringComparison.Ordinal)) {
            path = path[..^ApiPath.Length].TrimEnd(slash);
        }
        return parsed.GetLeftPart(UriPartial.Authority) + path + parsed.Query + parsed.Fragment;
    }

    public static AuthenticationHeaderValue? BasicAuth(string url) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) || parsed.UserInfo.Length == 0) {
            return null;
        }
        string[] parts = parsed.UserInfo.Split(userSeparator, 2);
        string user = Uri.UnescapeDataString(parts[0]);
        string password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        return new AuthenticationHeaderValue(
            basicScheme, Convert.ToBase64String(Encoding.UTF8.GetBytes(user + userSeparator + password)));
    }

    public static string NotRunning(string shownRoot) => $"No Ollama answered at {shownRoot}. Start it with: ollama serve";

    public static string WithoutUserinfo(string url) => Userinfo().Replace(url, schemePart);

    public static string CheckedRoot(string url) {
        string root = Root(url);
        return Uri.TryCreate(root, UriKind.Absolute, out Uri? parsed)
            && schemes.Contains(parsed.Scheme)
            && parsed.Host.Length > 0
            ? root
            : throw new ApiException(
                ApiErrorKind.Connection,
                $"ollama_url '{WithoutUserinfo(url)}' is not an http or https URL with a host");
    }

    [GeneratedRegex(@"^(?<scheme>[A-Za-z][A-Za-z0-9+.-]*:/*).*@", RegexOptions.Singleline)]
    private static partial Regex Userinfo();
}
