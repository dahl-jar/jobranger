namespace Jobranger.Cli.Features.Applications;

internal sealed record ApplicationChange(ApplicationStatus? Status = null, string? Note = null);
