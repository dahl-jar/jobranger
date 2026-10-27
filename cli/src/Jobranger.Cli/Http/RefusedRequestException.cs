namespace Jobranger.Cli.Http;

internal sealed class RefusedRequestException(string reason)
    : Exception($"request refused reason={reason}") {
    public string Reason { get; } = reason;
}
