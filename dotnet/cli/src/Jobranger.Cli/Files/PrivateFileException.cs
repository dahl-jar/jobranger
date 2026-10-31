namespace Jobranger.Cli.Files;

internal sealed class PrivateFileException(string path, string reason)
    : Exception($"file cannot be used path={path} reason={reason}") {
    public string Path { get; } = path;

    public string Reason { get; } = reason;

    public bool IsUnreadable { get; init; }
}
