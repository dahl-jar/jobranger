namespace Jobranger.Cli.Features.Matching;

internal sealed record PromptExample(
    string Action,
    string Title,
    string Company,
    string Summary,
    IReadOnlyList<string> Requirements,
    double Distance,
    long DecisionId);
