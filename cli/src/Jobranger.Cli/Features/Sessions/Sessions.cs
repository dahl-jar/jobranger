using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Jobranger.Contracts.Channels;
using Jobranger.Contracts.Envelopes;
using Jobranger.Contracts.Fields;
using Jobranger.Contracts.Limits;
using NSec.Cryptography;

namespace Jobranger.Cli.Features.Sessions;

internal sealed class Sessions(Key cliStatic, ChannelId channel, TimeProvider time) : IDisposable {
    public const string HelloKind = "hello";
    public const string DataKind = "data";

    private const int sidBytes = 16;
    private const long helloSeq = 0;
    private const string agentPunctuation = " ._()/-";

    private static readonly TimeSpan stepTimeout = TimeSpan.FromSeconds(ContractLimits.StepTimeoutSeconds);
    private static readonly TimeSpan confirmTimeout = TimeSpan.FromSeconds(ContractLimits.PairConfirmTimeoutSeconds);

    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public DateTimeOffset? NextDeadline => entries.Values.Min(entry => entry.Deadline);

    public bool Contains(string sid) => entries.ContainsKey(sid);

    public static CliHandshake Accept(Key cliStatic, string sid, HelloSteps.Commit commit) {
        if (!CanonicalBase64Url.TryDecode(sid, sidBytes, out byte[] sidRaw)) {
            throw new HandshakeException(HandshakeFailure.Step);
        }
        return new CliHandshake(cliStatic, sidRaw, commit, Key.Create(KeyAgreementAlgorithm.X25519));
    }

    public static byte[] PairTag(string code, ChannelId channel) {
        byte[] message = [.. Encoding.UTF8.GetBytes(ContractLimits.PairTagLabel), .. Base64Url.DecodeFromChars(channel.Value)];
        byte[] mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(code), message);
        return mac[..ContractLimits.PairTagBytes];
    }

    public static bool IsPairTag(string code, ChannelId channel, ReadOnlySpan<byte> pairTag) =>
        CryptographicOperations.FixedTimeEquals(PairTag(code, channel), pairTag);

    public static string CleanName(string name) {
        string kept = new([.. name.Where(IsDisplayChar)]);
        return kept.Length > ContractLimits.AgentDisplayMaxChars ? kept[..ContractLimits.AgentDisplayMaxChars] : kept;
    }

    public SessionUpdate Receive(Envelope envelope, Func<HelloSteps.Commit, CommitRoute> route) => envelope.Kind switch {
        HelloKind => Hello(envelope, route),
        DataKind => Data(envelope),
        _ => SessionUpdate.Empty,
    };

    public void Establish(string sid) {
        if (!entries.TryGetValue(sid, out Entry? entry) || entry.Session is null || !entry.Hold) {
            throw new InvalidOperationException("only a confirmed held session can be established");
        }
        entry.Open = true;
    }

    public Envelope Seal(string sid, byte[] plaintext) {
        Entry entry = entries.TryGetValue(sid, out Entry? found) && found.Open
            ? found
            : throw new InvalidOperationException("data goes only to an open session");
        (long seq, string ciphertext) = entry.Session!.Seal(plaintext);
        return new Envelope(ContractLimits.ContractVersion, channel.Value, sid, seq, DataKind, ciphertext);
    }

    public SessionUpdate Abort(string sid) => Close(sid, sendAbort: true);

    public SessionUpdate Drop(IReadOnlyList<string> sids) =>
        SessionUpdate.Join(sids.Select(sid => Close(sid, sendAbort: false)));

    public SessionUpdate Expire() {
        DateTimeOffset now = time.GetUtcNow();
        string[] expired = [.. entries.Where(pair => pair.Value.Deadline <= now).Select(pair => pair.Key)];
        return SessionUpdate.Join(expired.Select(sid => Close(sid, sendAbort: true)));
    }

    public void Dispose() {
        foreach (Entry entry in entries.Values) {
            entry.Dispose();
        }
        entries.Clear();
    }

    private static bool IsDisplayChar(char character) =>
        char.IsAsciiLetterOrDigit(character) || agentPunctuation.Contains(character, StringComparison.Ordinal);

    private SessionUpdate Hello(Envelope envelope, Func<HelloSteps.Commit, CommitRoute> route) {
        string sid = envelope.Sid;
        Entry? entry = entries.GetValueOrDefault(sid);
        HelloSteps.Step? step = envelope.Seq == helloSeq ? ParseOrNull(envelope.Ciphertext) : null;
        return (step, entry) switch {
            (null, null) => SessionUpdate.Empty,
            (null, _) => Close(sid, sendAbort: true),
            (HelloSteps.Abort, _) => Close(sid, sendAbort: false),
            (HelloSteps.Commit commit, null) => Begin(sid, commit, route(commit)),
            (_, null) => Reply(sid, new HelloSteps.UnknownSession()),
            (HelloSteps.Reveal reveal, { Handshake: CliHandshake handshake }) => OnReveal(sid, entry, handshake, reveal),
            (HelloSteps.Confirm confirm, { Handshake: CliHandshake handshake }) => OnConfirm(sid, entry, handshake, confirm),
            _ => Close(sid, sendAbort: true),
        };
    }

    private static HelloSteps.Step? ParseOrNull(string ciphertext) {
        try {
            return HelloSteps.Parse(ciphertext);
        } catch (HandshakeException) {
            return null;
        }
    }

    private SessionUpdate Begin(string sid, HelloSteps.Commit commit, CommitRoute route) {
        if (route == CommitRoute.Ignore) {
            return SessionUpdate.Empty;
        }
        if (route == CommitRoute.Abort) {
            return Reply(sid, new HelloSteps.Abort());
        }
        CliHandshake handshake;
        try {
            handshake = Accept(cliStatic, sid, commit);
        } catch (HandshakeException) {
            return Reply(sid, new HelloSteps.Abort());
        }
        entries[sid] = new Entry(handshake, route == CommitRoute.Hold) { Deadline = time.GetUtcNow() + stepTimeout };
        return Reply(sid, handshake.Offer());
    }

    private SessionUpdate OnReveal(string sid, Entry entry, CliHandshake handshake, HelloSteps.Reveal reveal) {
        HelloSteps.Confirm confirm;
        try {
            confirm = handshake.OnReveal(reveal);
        } catch (HandshakeException) {
            return Close(sid, sendAbort: true);
        }
        entry.Deadline = time.GetUtcNow() + (entry.Hold ? confirmTimeout : stepTimeout);
        SessionEvent revealed = new SessionEvent.Revealed(sid, handshake.Words, CleanName(handshake.Agent));
        return new SessionUpdate([Hello(sid, confirm)], [revealed]);
    }

    private SessionUpdate OnConfirm(string sid, Entry entry, CliHandshake handshake, HelloSteps.Confirm confirm) {
        LinkSession session;
        try {
            session = handshake.OnConfirm(confirm);
        } catch (HandshakeException) {
            return Close(sid, sendAbort: true);
        }
        entry.Confirm(session);
        return new SessionUpdate([], [new SessionEvent.Confirmed(sid)]);
    }

    private SessionUpdate Data(Envelope envelope) {
        string sid = envelope.Sid;
        if (!entries.TryGetValue(sid, out Entry? entry)) {
            return Reply(sid, new HelloSteps.UnknownSession());
        }
        if (!entry.Open) {
            return Close(sid, sendAbort: true);
        }
        try {
            byte[] plaintext = entry.Session!.Open(envelope.Seq, envelope.Ciphertext);
            return new SessionUpdate([], [new SessionEvent.Data(sid, plaintext)]);
        } catch (FrameException) {
            return Close(sid, sendAbort: true);
        }
    }

    private SessionUpdate Close(string sid, bool sendAbort) {
        if (!entries.Remove(sid, out Entry? entry)) {
            return SessionUpdate.Empty;
        }
        entry.Dispose();
        IReadOnlyList<Envelope> replies = sendAbort ? [Hello(sid, new HelloSteps.Abort())] : [];
        return new SessionUpdate(replies, [new SessionEvent.Closed(sid)]);
    }

    private SessionUpdate Reply(string sid, HelloSteps.Step step) => new([Hello(sid, step)], []);

    private Envelope Hello(string sid, HelloSteps.Step step) =>
        new(ContractLimits.ContractVersion, channel.Value, sid, helloSeq, HelloKind, HelloSteps.Encode(step));

    private sealed class Entry(CliHandshake handshake, bool hold) : IDisposable {
        public CliHandshake? Handshake { get; private set; } = handshake;

        public LinkSession? Session { get; private set; }

        public bool Hold { get; } = hold;

        public bool Open { get; set; }

        public DateTimeOffset? Deadline { get; set; }

        public void Confirm(LinkSession session) {
            Handshake?.Dispose();
            Handshake = null;
            Session = session;
            Deadline = null;
            Open = !Hold;
        }

        public void Dispose() {
            Handshake?.Dispose();
            Session?.Dispose();
        }
    }
}
