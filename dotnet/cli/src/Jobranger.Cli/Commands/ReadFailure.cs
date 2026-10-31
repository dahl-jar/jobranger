namespace Jobranger.Cli.Commands;

internal sealed record ReadFailure(string Code, IReadOnlyList<string> Lines);
