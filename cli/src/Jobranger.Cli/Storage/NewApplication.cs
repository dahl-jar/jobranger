namespace Jobranger.Cli.Storage;

internal sealed record NewApplication(
    string Company,
    string Role,
    string Place,
    string? Deadline,
    string Note,
    DateTimeOffset AppliedAt,
    string? CvName,
    string? AdId);
