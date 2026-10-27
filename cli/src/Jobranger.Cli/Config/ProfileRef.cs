namespace Jobranger.Cli.Config;

internal sealed record ProfileRef(string Path, string? CvName = null, string? CvReadAt = null);
