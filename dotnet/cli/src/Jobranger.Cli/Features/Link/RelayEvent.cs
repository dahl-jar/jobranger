using Jobranger.Messaging.Envelopes;

namespace Jobranger.Cli.Features.Link;

internal abstract record RelayEvent {
    internal sealed record Received(Envelope Envelope) : RelayEvent;

    internal sealed record SessionsClosed(IReadOnlyList<string> Sids) : RelayEvent;

    internal sealed record Replaced : RelayEvent;

    internal sealed record Disconnected : RelayEvent;
}
