namespace Jobranger.Cli.Features.Sources;

internal sealed class SourceException(string message, SourceBatch? partial, Exception? cause = null)
    : Exception(message, cause) {
    public SourceBatch? Partial { get; } = partial;
}
