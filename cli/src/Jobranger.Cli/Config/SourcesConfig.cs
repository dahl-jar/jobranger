using Jobranger.Cli.Catalogue;

namespace Jobranger.Cli.Config;

internal sealed record SourcesConfig {
    public bool Nav { get; init; } = true;

    public bool Employers { get; init; } = true;

    public bool Finn { get; init; }

    public bool LinkedIn { get; init; }

    public IReadOnlyList<EmployerFeed> EmployerFeeds { get; init; } = [];
}
