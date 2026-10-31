using SourcesEntry = Jobranger.Cli.Features.Sources.Sources;

namespace Jobranger.Cli.Features.Runs;

internal static class SourceDeadline {
    private static readonly TimeSpan standard = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan finn = TimeSpan.FromMinutes(40);

    public static TimeSpan For(string source) => source == SourcesEntry.Finn ? finn : standard;
}
