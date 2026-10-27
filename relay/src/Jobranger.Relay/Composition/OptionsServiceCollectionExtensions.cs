using Microsoft.Extensions.Options;

namespace Jobranger.Relay.Composition;

internal static class OptionsServiceCollectionExtensions {
    public static IServiceCollection AddRelayOptions(this IServiceCollection services) {
        services.AddOptions<RelayOptions>().BindConfiguration(RelayOptions.Section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<RelayOptions>, RelayOptionsValidator>();
        return services;
    }
}
