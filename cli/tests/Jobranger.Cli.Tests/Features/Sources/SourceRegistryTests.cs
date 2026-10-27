using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Sources;
using static Jobranger.Cli.Tests.Mocks.SourceFixtures;

namespace Jobranger.Cli.Tests.Features.Sources;

public sealed class SourceRegistryTests {
    private static readonly IReadOnlyList<EmployerFeed> feeds = [AshbyFeed, TeamtailorFeed];

    [Fact]
    public void ShouldAddLinkedIn_WhenConfigTurnsItOn() {
        SourcesConfig config = new() { Nav = false, Employers = false, LinkedIn = true };

        IReadOnlyList<ISource> sources = SourceRegistry.Enabled(config);

        Assert.Equal(["linkedin"], sources.Select(source => source.Name));
    }

    [Fact]
    public void ShouldAddSourcePerFeed_WhenEmployersAreOn() {
        SourcesConfig config = new() { Nav = false, Employers = true, EmployerFeeds = feeds };

        IReadOnlyList<ISource> sources = SourceRegistry.Enabled(config);

        Assert.Equal(
            [("ashby:example-one", typeof(AshbySource)), ("teamtailor:example", typeof(TeamtailorSource))],
            sources.Select(source => (source.Name, source.GetType())));
    }

    [Fact]
    public void ShouldAddNoFeed_WhenEmployersAreOff() {
        SourcesConfig config = new() { Nav = false, Employers = false, EmployerFeeds = feeds };

        IReadOnlyList<ISource> sources = SourceRegistry.Enabled(config);

        Assert.Empty(sources);
    }
}
