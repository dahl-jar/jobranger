namespace Jobranger.Cli.Config;

internal sealed record Targets {
    public static readonly IReadOnlyList<string> AllRoles = [
        "backend",
        "frontend",
        "fullstack",
        "mobile",
        "data_engineering",
        "devops_platform",
        "ml_ai",
        "embedded",
        "game",
    ];

    public IReadOnlyList<string> Subfields { get; init; } = [];

    public Limits Limits { get; init; } = new();

    public IReadOnlyList<string> Places { get; init; } = [];

    public bool Relocate { get; init; } = true;
}
