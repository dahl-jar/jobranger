using Jobranger.Relay.Composition;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Jobranger.Relay.Security;

internal static class SiteCors {
    public const string Policy = "site";

    public static IServiceCollection AddSiteCors(this IServiceCollection services) {
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<RelayOptions>>((cors, relay) =>
            cors.AddPolicy(Policy, policy => policy
                .WithOrigins(relay.Value.SiteOrigin)
                .WithMethods(HttpMethods.Post)
                .WithHeaders(HeaderNames.ContentType)
                .WithExposedHeaders(HeaderNames.RetryAfter)));
        return services;
    }
}
