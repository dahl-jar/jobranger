using System.Buffers.Text;
using System.Net;
using Jobranger.Relay.Core.Limits;
using Microsoft.Extensions.Options;

namespace Jobranger.Relay.Composition;

public sealed class RelayOptionsValidator : IValidateOptions<RelayOptions> {
    public ValidateOptionsResult Validate(string? name, RelayOptions options) {
        List<string> failures = [];
        if (!IsOrigin(options.SiteOrigin)) {
            failures.Add("Relay:SiteOrigin must be an http or https origin with no path");
        }

        if (!IsKey(options.HandleKey)) {
            failures.Add("Relay:HandleKey must be base64 of at least 32 bytes");
        }

        if (!string.IsNullOrEmpty(options.PreviousHandleKey) && !IsKey(options.PreviousHandleKey)) {
            failures.Add("Relay:PreviousHandleKey must be empty or base64 of at least 32 bytes");
        }

        if (!IsKey(options.RedactionKey)) {
            failures.Add("Relay:RedactionKey must be base64 of at least 32 bytes");
        }

        if (options.KnownProxyNetworks.Count == 0 || !options.KnownProxyNetworks.All(network => IPNetwork.TryParse(network, out _))) {
            failures.Add("Relay:KnownProxyNetworks must list at least one network in CIDR form");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsOrigin(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && string.Equals(uri.GetLeftPart(UriPartial.Authority), value, StringComparison.Ordinal);

    private static bool IsKey(string value) =>
        Base64.IsValid(value, out int decodedLength) && decodedLength >= RelayLimits.MinHandleKeyBytes;
}
