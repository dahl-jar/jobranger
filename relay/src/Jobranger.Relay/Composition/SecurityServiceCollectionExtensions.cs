using Jobranger.Relay.Security;

namespace Jobranger.Relay.Composition;

internal static class SecurityServiceCollectionExtensions {
    public static IServiceCollection AddRelaySecurity(this IServiceCollection services) {
        services.AddProxyHeaders();
        services.AddSiteCors();
        services.AddRelayRateLimits();
        return services;
    }
}
