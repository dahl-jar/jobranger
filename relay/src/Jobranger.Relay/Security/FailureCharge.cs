using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Jobranger.Relay.Security;

internal static class FailureCharge
{
    private const string chargedKey = "relay.failure-charged";

    public static ProblemHttpResult? Charge(HttpContext http)
    {
        if (http.RequestServices.GetRequiredService<FailurePolicies>().For(http) is not { } policy
            || http.Items.ContainsKey(chargedKey))
        {
            return null;
        }

        http.Items[chargedKey] = true;
        using RateLimitLease lease = http.RequestServices.GetRequiredService<RelayRateLimiter>().AttemptAcquireForAddress(http, policy);
        return lease.IsAcquired ? null : RateLimiting.Refuse(http, lease, policy);
    }

    public static async Task ChargeBadRequests(HttpContext http, RequestDelegate next)
    {
        await next(http);
        if (http.Response is
            {
                StatusCode: StatusCodes.Status400BadRequest
                    or StatusCodes.Status413PayloadTooLarge
                    or StatusCodes.Status415UnsupportedMediaType,
                HasStarted: false,
            }
            && Charge(http) is { } refused)
        {
            await refused.ExecuteAsync(http);
        }
    }

    public static TBuilder RequireFailureLimit<TBuilder>(this TBuilder builder, string policy)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new RelayFailureLimit(policy));
}
