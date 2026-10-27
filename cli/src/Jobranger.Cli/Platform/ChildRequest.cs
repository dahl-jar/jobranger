namespace Jobranger.Cli.Platform;

internal sealed record ChildRequest(
    string Program,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    string WorkingDirectory,
    ReadOnlyMemory<byte> Input,
    int OutputLimit,
    TimeSpan Deadline);
