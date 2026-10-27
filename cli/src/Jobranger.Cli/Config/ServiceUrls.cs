using System.Collections;
using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Config;

internal sealed record ServiceUrls(Uri Site, Uri Relay) {
    public const string SiteVariable = "JOBRANGER_SITE_URL";
    public const string RelayVariable = "JOBRANGER_RELAY_URL";

    private const string defaultSite = "https://jobranger.no";
    private const string defaultRelay = "https://relay.jobranger.no";
    private const string environmentSource = "environment";
    private const char slash = '/';

    public static ServiceUrls From(IDictionary environment) {
        List<ConfigError> errors = [];
        Uri site = Read(environment, SiteVariable, defaultSite, errors);
        Uri relay = Read(environment, RelayVariable, defaultRelay, errors);
        if (errors.Count > 0) {
            throw new ConfigInvalidException(environmentSource, errors);
        }
        return new ServiceUrls(site, relay);
    }

    private static Uri Read(IDictionary environment, string name, string fallback, List<ConfigError> errors) {
        string text = EnvironmentVariables.Read(environment, name) ?? fallback;
        if (Uri.TryCreate(text.TrimEnd(slash), UriKind.Absolute, out Uri? url)
            && IsAllowedScheme(url)
            && url.UserInfo.Length == 0
            && url.Query.Length == 0
            && url.Fragment.Length == 0) {
            return url;
        }
        errors.Add(new ConfigError(name, ConfigProblem.NotServiceUrl));
        return new Uri(fallback);
    }

    private static bool IsAllowedScheme(Uri url) =>
        url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp && url.IsLoopback;
}
