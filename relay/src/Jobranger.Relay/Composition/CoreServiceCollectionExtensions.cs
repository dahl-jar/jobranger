using System.Text.Json.Serialization;
using Jobranger.Relay.Core.Limits;
using Jobranger.Relay.Core.Nonces;
using Jobranger.Relay.Core.Pairing;
using Jobranger.Relay.Core.Tokens;
using Jobranger.Relay.Endpoints;
using Jobranger.Relay.Health;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace Jobranger.Relay.Composition;

internal static class CoreServiceCollectionExtensions {
    public static IServiceCollection AddRelayCore(this IServiceCollection services) {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<RelayOptions>>().Value.ToHandleKeys());
        services.AddSingleton<ChannelHandle>();
        services.AddSingleton(provider => new PairingStore(provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton(provider => new NonceStore(provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton(provider => new PairedChannels(provider.GetRequiredService<TimeProvider>()));
        services.ConfigureHttpJsonOptions(json => {
            json.SerializerOptions.TypeInfoResolverChain.Insert(0, RelayJsonContext.Default);
            json.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            json.SerializerOptions.AllowDuplicateProperties = false;
        });
        services.Configure<KestrelServerOptions>(kestrel => kestrel.Limits.MaxRequestBodySize = RelayLimits.MaxRequestBodyBytes);
        services.Configure<RouteHandlerOptions>(routes => routes.ThrowOnBadRequest = false);
        services.AddProblemDetails(problems => problems.CustomizeProblemDetails = RelayProblems.Customize);
        services.AddRelayHealth();
        return services;
    }
}
