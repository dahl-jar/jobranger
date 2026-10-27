namespace Jobranger.Cli.Features.Profiles;

internal sealed record Profile {
    public required string Summary { get; init; }

    public required double ProfessionalYears { get; init; }

    public bool HasMaster { get; init; }

    public IReadOnlyList<Evidence> Education { get; init; } = [];

    public IReadOnlyList<Evidence> Experience { get; init; } = [];

    public IReadOnlyList<Evidence> Projects { get; init; } = [];

    public IReadOnlyList<string> Languages { get; init; } = [];

    public IReadOnlyList<string> Stack { get; init; } = [];
}
