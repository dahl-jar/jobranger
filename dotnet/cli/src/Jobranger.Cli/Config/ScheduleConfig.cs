namespace Jobranger.Cli.Config;

internal sealed record ScheduleConfig {
    public IReadOnlyList<string> Times { get; init; } = ["03:00", "15:00"];
}
