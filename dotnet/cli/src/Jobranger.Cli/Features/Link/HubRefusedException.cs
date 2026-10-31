namespace Jobranger.Cli.Features.Link;

internal sealed class HubRefusedException(string reason) : Exception($"relay hub refused the frame reason={reason}") {
    public string Reason { get; } = reason;
}
