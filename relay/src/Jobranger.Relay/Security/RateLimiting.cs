using System.Globalization;
using System.Threading.RateLimiting;
using Jobranger.Contracts.Channels;
using Jobranger.Relay.Core.Limits;
using Jobranger.Relay.Core.Sweeping;
using Jobranger.Relay.Core.Tokens;
using Jobranger.Relay.Endpoints;
using Jobranger.Relay.Telemetry;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;

namespace Jobranger.Relay.Security;

internal static class RateLimiting {
    public const string Nonce = "nonce";
    public const string PairStart = "pair-start";
    public const string PairClaim = "pair-claim";
    public const string Session = "session";
    public const string SessionChannel = "session-channel";
    public const string ChannelNonce = "channel-nonce";
    public const string HubSockets = "hub-sockets";

    private const int minRetryAfterSeconds = 1;

    public static readonly string[] Policies = [Nonce, PairStart, PairClaim, Session, HubSockets];

    public static IServiceCollection AddRelayRateLimits(this IServiceCollection services) {
        services.AddSingleton(provider => new RelayRateLimiter(provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ISweepable>(provider => provider.GetRequiredService<RelayRateLimiter>());
        services.AddSingleton<RefusalSampler>();
        services.AddSingleton<FailurePolicies>();
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<RelayRateLimiter>((options, limiter) => {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejection;
            options.GlobalLimiter = limiter;
        });
        return services;
    }

    public static TBuilder RequireRelayLimit<TBuilder>(this TBuilder builder, string policy)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new RelayRateLimit(policy));

    public static RateLimiter Limiter(string policy, int scale, TimeProvider time) => policy switch {
        PairClaim => new BucketLimiter(
            RelayLimits.ClaimBurst * scale, RelayLimits.ClaimRefillTokens * scale, RelayLimits.ClaimRefillPeriod, time),
        PairStart => new WindowLimiter(RelayLimits.PairStartsPerWindow * scale, RelayLimits.PairStartWindow, time),
        Session => new WindowLimiter(RelayLimits.FailedSessionsPerWindow * scale, RelayLimits.SessionRequestWindow, time),
        SessionChannel or ChannelNonce => new WindowLimiter(
            RelayLimits.SessionsPerChannelWindow * scale, RelayLimits.SessionRequestWindow, time),
        Nonce => new WindowLimiter(RelayLimits.NonceRequestsPerWindow * scale, RelayLimits.NonceRequestWindow, time),
        HubSockets => new ConcurrencyLimiter(new ConcurrencyLimiterOptions {
            PermitLimit = RelayLimits.HubSocketsPerClient * scale,
            QueueLimit = 0,
        }),
        _ => throw new ArgumentOutOfRangeException(
            nameof(policy), policy, "policy must be nonce, pair-start, pair-claim, session, session-channel, channel-nonce or hub-sockets"),
    };

    public static ProblemHttpResult? Throttle(HttpContext http, RateLimitLease lease, string policy) {
        using (lease) {
            return lease.IsAcquired ? null : Refuse(http, lease, policy);
        }
    }

    public static ProblemHttpResult? ThrottleChannel(HttpContext http, RelayRateLimiter limiter, ChannelId channel, RelayRole role) =>
        Throttle(http, limiter.AttemptAcquireForChannel(http, channel, role), SessionChannel);

    public static ProblemHttpResult Refuse(HttpContext http, RateLimitLease lease, string? policy) {
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)) {
            int seconds = Math.Max(minRetryAfterSeconds, (int)Math.Ceiling(retryAfter.TotalSeconds));
            http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        if (policy is not null) {
            http.RequestServices.GetRequiredService<RelayMetrics>().RateLimited(policy);
            if (http.RequestServices.GetRequiredService<RefusalSampler>().ShouldLog(policy)) {
                RelayLog.For(http.RequestServices.GetRequiredService<ILoggerFactory>()).RateLimitRefused(policy);
            }
        }

        return RelayProblems.RateLimited.ToResult();
    }

    private static async ValueTask WriteRejection(OnRejectedContext context, CancellationToken cancellation) =>
        await Refuse(
            context.HttpContext,
            context.Lease,
            context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<RelayRateLimit>()?.Policy)
        .ExecuteAsync(context.HttpContext);
}
