namespace Jobranger.Cli.Features.Runs;

internal sealed record PromptSetup(
    string System, string Version, IReadOnlySet<string> Evidence, IReadOnlySet<string> Education);
