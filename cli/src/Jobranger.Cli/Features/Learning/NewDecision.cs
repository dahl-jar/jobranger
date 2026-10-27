namespace Jobranger.Cli.Features.Learning;

internal sealed record NewDecision(
    string AdId,
    string DedupKey,
    DecisionAction Action,
    DateTimeOffset DecidedAt,
    string Title,
    string Company,
    string Summary,
    IReadOnlyList<string> Requirements,
    string EmbedText);
