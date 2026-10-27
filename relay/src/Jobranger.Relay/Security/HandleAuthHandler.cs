using System.Text.Encodings.Web;
using Jobranger.Relay.Core;
using Jobranger.Relay.Hubs;
using Jobranger.Relay.Options;
using Jobranger.Relay.Telemetry;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Jobranger.Relay.Security;

internal sealed class HandleAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggers,
    UrlEncoder encoder,
    ChannelHandle handles,
    IOptions<RelayOptions> relay,
    RefusalSampler sampler,
    RelayMetrics metrics)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, NullLoggerFactory.Instance, encoder)
{
    public const string SchemeName = "RelayHandle";
    public const string HubPath = "/hub";

    private const string tokenQuery = "access_token";
    private const string invalidHandle = "invalid_handle";
    private const string wrongOrigin = "wrong_origin";
    private const string missingHandle = "missing_handle";
    private const string samplerPrefix = "hub-auth:";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Path.StartsWithSegments(HubPath) || Request.Query[tokenQuery] is not [{ } token])
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (handles.Verify(token) is not { } claims)
        {
            return Task.FromResult(AuthenticateResult.Fail(invalidHandle));
        }

        StringValues origins = Request.Headers.Origin;
        bool fromSite = origins is [{ } origin] && string.Equals(origin, relay.Value.SiteOrigin, StringComparison.Ordinal);
        if (!fromSite && (origins.Count > 0 || claims.Role == RelayRole.Web))
        {
            return Task.FromResult(AuthenticateResult.Fail(wrongOrigin));
        }

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(HubClaims.Principal(claims, SchemeName), SchemeName)));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        AuthenticateResult result = await HandleAuthenticateOnceSafeAsync();
        string reason = result.Failure?.Message ?? missingHandle;
        metrics.ConnectionRejected(reason);
        if (sampler.ShouldLog(samplerPrefix + reason))
        {
            RelayLog.For(loggers).HubAuthRefused(reason);
        }

        if (FailureCharge.Charge(Context) is { } refused)
        {
            await refused.ExecuteAsync(Context);
            return;
        }

        await base.HandleChallengeAsync(properties);
    }
}
