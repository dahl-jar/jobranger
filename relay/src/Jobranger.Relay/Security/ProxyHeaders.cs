using Jobranger.Relay.Composition;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Jobranger.Relay.Security;

internal static class ProxyHeaders {
    private const string clientAddressHeader = "CF-Connecting-IP";
    private const int trustedHops = 1;

    public static IServiceCollection AddProxyHeaders(this IServiceCollection services) {
        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<RelayOptions>>((forwarded, relay) => {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            forwarded.ForwardedForHeaderName = clientAddressHeader;
            forwarded.ForwardLimit = trustedHops;
            forwarded.KnownProxies.Clear();
            forwarded.KnownIPNetworks.Clear();
            foreach (string network in relay.Value.KnownProxyNetworks) {
                forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });
        return services;
    }
}
