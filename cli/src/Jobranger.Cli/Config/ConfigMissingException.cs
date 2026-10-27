namespace Jobranger.Cli.Config;

internal sealed class ConfigMissingException(string path, Exception innerException)
    : Exception($"file not found: {path}", innerException) {
    public string Path { get; } = path;
}
