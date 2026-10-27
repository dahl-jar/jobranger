namespace Jobranger.Cli.Features.Applications;

internal sealed record Application(
    long Id,
    string? AdId,
    string Company,
    string Role,
    string Place,
    string? Deadline,
    ApplicationStatus Status,
    string Note,
    string AppliedAt,
    string? CvName,
    string? RemovedAt,
    IReadOnlyList<ApplicationEvent> Events);
