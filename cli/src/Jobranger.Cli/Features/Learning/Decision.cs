namespace Jobranger.Cli.Features.Learning;

internal sealed record Decision(
    long Id,
    string AdId,
    string DedupKey,
    DecisionAction Action,
    string DecidedAt,
    string Title,
    string Company,
    string Summary,
    IReadOnlyList<string> Requirements,
    string EmbedText,
    string? EmbeddedModel);
