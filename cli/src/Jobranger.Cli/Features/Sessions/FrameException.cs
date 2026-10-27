namespace Jobranger.Cli.Features.Sessions;

internal sealed class FrameException(FrameFailure reason) : Exception($"frame rejected reason={reason}") {
    public FrameFailure Reason { get; } = reason;
}
