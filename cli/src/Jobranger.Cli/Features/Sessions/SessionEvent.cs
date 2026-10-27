namespace Jobranger.Cli.Features.Sessions;

internal abstract record SessionEvent(string Sid) {
    internal sealed record Revealed(string Sid, IReadOnlyList<string> Words, string Agent) : SessionEvent(Sid);

    internal sealed record Confirmed(string Sid) : SessionEvent(Sid);

    internal sealed record Data(string Sid, byte[] Plaintext) : SessionEvent(Sid);

    internal sealed record Closed(string Sid) : SessionEvent(Sid);
}
