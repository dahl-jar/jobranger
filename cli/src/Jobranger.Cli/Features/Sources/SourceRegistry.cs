using Jobranger.Cli.Config;

namespace Jobranger.Cli.Features.Sources;

internal static class SourceRegistry {
    public static IReadOnlyList<ISource> Enabled(SourcesConfig sources) {
        List<ISource> enabled = [];
        if (sources.Finn) {
            enabled.Add(new FinnSource());
        }
        if (sources.Nav) {
            enabled.Add(new NavSource());
        }
        return enabled;
    }
}
