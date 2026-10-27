namespace Jobranger.Cli.Storage;

internal sealed record Migration(int Version, IReadOnlyList<string> Statements);
