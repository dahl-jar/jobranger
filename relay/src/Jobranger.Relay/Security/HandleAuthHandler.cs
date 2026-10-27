using System.Text.Encodings.Web;
using Jobranger.Relay.Composition;
using Jobranger.Relay.Core.Tokens;
using Jobranger.Relay.Endpoints;
using Jobranger.Relay.Hubs;
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
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, NullLoggerFactory.Instance, encoder) {
    public const string SchemeName = "RelayHandle";
    public const string HubPath = "/hub";

    private const string tokenQuery = "access_token";
    private const string samplerPrefix = "hub-auth:";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        if (!Request.Path.StartsWithSegments(HubPath) || Request.Query[tokenQuery] is not [{ } token]) {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (handles.Verify(token) is not { } claims) {
            return Task.FromResult(AuthenticateResult.Fail(RelayProblems.InvalidHandle.Code));
        }

        StringValues origins = Request.Headers.Origin;
        bool fromSite = origins is [{ } origin] && string.Equals(origin, relay.Value.SiteOrigin, StringComparison.Ordinal);
        if (!fromSite && (origins.Count > 0 || claims.Role == RelayRole.Web)) {
            return Task.FromResult(AuthenticateResult.Fail(RelayProblems.WrongOrigin.Code));
        }

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(HubClaims.Principal(claims, SchemeName), SchemeName)));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties) {
        RelayProblem problem = Refusal(await HandleAuthenticateOnceSafeAsync());
        metrics.ConnectionRejected(problem.Code);
        if (sampler.ShouldLog(samplerPrefix + problem.Code)) {
            RelayLog.For(loggers).HubAuthRefused(problem.Code);
        }

        await (FailureCharge.Charge(Context) ?? problem.ToResult()).ExecuteAsync(Context);
    }

    private static RelayProblem Refusal(AuthenticateResult result) => result.Failure?.Message switch {
        null => RelayProblems.MissingHandle,
        string reason when reason == RelayProblems.WrongOrigin.Code => RelayProblems.WrongOrigin,
        _ => RelayProblems.InvalidHandle,
    };
}
