using System.Text.RegularExpressions;
using Jobranger.Relay.Composition;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Options;

namespace Jobranger.Relay.Telemetry;

internal static partial class RedactionSetup {
    private const int redactionKeyId = 1;
    private const string redactedToken = "[redacted]";

    public static WebApplicationBuilder AddRelayRedaction(this WebApplicationBuilder builder) {
#pragma warning disable EXTEXP0002
        builder.Services.AddRedaction(redaction =>
            redaction.SetHmacRedactor(_ => { }, new DataClassificationSet(RelayTaxonomy.Pseudonymous)));
        builder.Services.AddOptions<HmacRedactorOptions>().Configure<IOptions<RelayOptions>>((hmac, relay) => {
            hmac.Key = relay.Value.RedactionKey;
            hmac.KeyId = redactionKeyId;
        });
#pragma warning restore EXTEXP0002
        builder.Logging.EnableRedaction();
        return builder;
    }

    public static string? RedactAccessTokens(string? text) =>
        text is null ? null : AccessTokenValue().Replace(text, redactedToken);

    [GeneratedRegex("(?<=access_token=)[^&\\s\"]+", RegexOptions.IgnoreCase)]
    private static partial Regex AccessTokenValue();
}
