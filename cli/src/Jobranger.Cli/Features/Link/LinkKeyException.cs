namespace Jobranger.Cli.Features.Link;

internal sealed class LinkKeyException : Exception {
    public LinkKeyException(string message)
        : base(message) {
    }

    public LinkKeyException(string message, Exception innerException)
        : base(message, innerException) {
    }
}
