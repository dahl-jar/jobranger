namespace Jobranger.Cli.Config;

internal sealed record Limits {
    public static readonly IReadOnlyList<string> AllLevels = ["junior", "mid", "senior", "lead", "none"];
    public static readonly IReadOnlyList<string> AllYears = ["0-1", "1-3", "3-6", "6+"];

    public IReadOnlyList<string> Levels { get; init; } = ["junior", "mid", "none"];

    public IReadOnlyList<string> Years { get; init; } = ["0-1", "1-3"];

    public bool HideMasters { get; init; } = true;
}
