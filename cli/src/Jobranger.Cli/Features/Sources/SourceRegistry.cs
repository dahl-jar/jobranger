using Jobranger.Cli.Catalogue;
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
        if (sources.LinkedIn) {
            enabled.Add(new LinkedInSource());
        }
        if (sources.Employers) {
            enabled.AddRange(sources.EmployerFeeds.Select(FeedSource));
        }
        return enabled;
    }

    private static ISource FeedSource(EmployerFeed feed) =>
        feed.Kind == AshbySource.Kind ? new AshbySource(feed) : new TeamtailorSource(feed);
}
