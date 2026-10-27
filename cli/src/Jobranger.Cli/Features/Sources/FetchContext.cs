using Jobranger.Cli.Catalogue;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Features.Sources;

internal sealed record FetchContext {
    public required HttpClient Http { get; init; }

    public required SourcePlan Plan { get; init; }

    public required DateTimeOffset RunStarted { get; init; }

    public required IReadOnlySet<string> KnownIds { get; init; }

    public required IReadOnlyDictionary<string, string> Cursors { get; init; }

    public required ILogger Logger { get; init; }

    public IReadOnlyList<SourceListing> PendingPages { get; init; } = [];
}
