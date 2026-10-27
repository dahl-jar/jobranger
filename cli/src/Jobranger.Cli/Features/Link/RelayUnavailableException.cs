namespace Jobranger.Cli.Features.Link;

internal sealed class RelayUnavailableException : Exception {
    public RelayUnavailableException(string message)
        : base(message) {
    }

    public RelayUnavailableException(string message, Exception innerException)
        : base(message, innerException) {
    }
}
