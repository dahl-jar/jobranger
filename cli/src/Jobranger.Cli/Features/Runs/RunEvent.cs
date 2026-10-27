namespace Jobranger.Cli.Features.Runs;

internal sealed record RunEvent(
    RunStage Stage,
    string? Source = null,
    int? FoundNew = null,
    int? Checked = null,
    int? Total = null);
