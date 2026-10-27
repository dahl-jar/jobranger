using Jobranger.Contracts.Envelopes;

namespace Jobranger.Cli.Features.Sessions;

internal sealed record SessionUpdate(IReadOnlyList<Envelope> Replies, IReadOnlyList<SessionEvent> Events) {
    public static SessionUpdate Empty { get; } = new([], []);

    public static SessionUpdate Join(IEnumerable<SessionUpdate> updates) {
        SessionUpdate[] all = [.. updates];
        return new SessionUpdate([.. all.SelectMany(update => update.Replies)], [.. all.SelectMany(update => update.Events)]);
    }
}
