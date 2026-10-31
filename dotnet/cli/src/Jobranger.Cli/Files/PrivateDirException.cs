namespace Jobranger.Cli.Files;

internal sealed class PrivateDirException(string path, Exception cause) : IOException(cause.Message, cause) {
    public string Path { get; } = path;
}
