using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Config;

namespace Jobranger.Cli.Features.Runs;

internal sealed record ReportInput(
    Targets Targets,
    string Version,
    DateOnly Today,
    DateTimeOffset StartedAt,
    IReadOnlyList<EmployerFeed> ReadFeeds,
    IReadOnlyDictionary<string, string> JobTitles);
