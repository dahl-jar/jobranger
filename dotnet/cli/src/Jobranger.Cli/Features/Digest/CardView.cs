namespace Jobranger.Cli.Features.Digest;

internal sealed record CardView(
    string Id,
    string Due,
    string DueNote,
    DigestFit Fit,
    string FitLabel,
    string Company,
    string Title,
    string Meta,
    string Why,
    IReadOnlyList<LinkView> Links);
