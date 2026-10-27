namespace Jobranger.Cli.Features.Sessions;

internal sealed class HandshakeException(HandshakeFailure reason) : Exception($"handshake aborted reason={reason}") {
    public HandshakeFailure Reason { get; } = reason;
}
