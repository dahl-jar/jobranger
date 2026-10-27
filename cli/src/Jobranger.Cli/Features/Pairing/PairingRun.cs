using System.Buffers.Text;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Features.Sessions;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;
using Jobranger.Contracts.Channels;
using Jobranger.Contracts.Envelopes;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;
using Jobranger.Contracts.Runs;
using Microsoft.Extensions.Logging;
using SessionTable = Jobranger.Cli.Features.Sessions.Sessions;

namespace Jobranger.Cli.Features.Pairing;

[UnsupportedOSPlatform("windows")]
internal sealed class PairingRun(
    Link.Link link,
    SessionTable sessions,
    IMessages messages,
    ChannelId channel,
    PairingStart start,
    string machine,
    TimeProvider time,
    ILogger logger) {
    private const string indent = "  ";
    private const char wordSeparator = ' ';
    private const string machineFallback = "computer";
    private const string browserFallback = "a browser";
    private const string candidateFailure = "candidate_closed";
    private const string secondCommitFailure = "second_commit";
    private const string terminalNoFailure = "terminal_no";
    private const string terminalTimeoutFailure = "terminal_timeout";
    private const string expiredFailure = "code_expired";
    private const string disconnectedFailure = "disconnected";
    private const string cancelledFailure = "cancelled";

    private static readonly TimeSpan answerTimeout = TimeSpan.FromSeconds(ContractLimits.PairConfirmTimeoutSeconds);

    private readonly HashSet<string> paired = [.. link.Devices().Select(device => device.Id)];
    private string? candidate;
    private byte[] candidateKey = [];
    private string agent = "";
    private bool secondCommit;
    private bool browserConfirmed;
    private bool terminalYes;
    private DateTimeOffset? answerDeadline;
    private Task<string>? answer;

    public async Task<int> RunAsync(TextWriter output, Func<string, string> ask, CancellationToken cancellationToken) {
        try {
            return await ListenAsync(output, ask, cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && candidate is not null) {
            await SendAsync(sessions.Abort(candidate), CancellationToken.None).ConfigureAwait(false);
            LogEvents.PairingFailed(logger, cancelledFailure);
            throw;
        }
    }

    private async Task<int> ListenAsync(TextWriter output, Func<string, string> ask, CancellationToken cancellationToken) {
        Task<RelayEvent> received = link.ReceiveAsync(cancellationToken).AsTask();
        while (true) {
            using CancellationTokenSource waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task timer = Task.Delay(UntilNextDeadline(), time, waiting.Token);
            Task done = await Task.WhenAny(timer, answer ?? Task.Delay(Timeout.Infinite, waiting.Token), received)
                .ConfigureAwait(false);
            await waiting.CancelAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            int? outcome;
            if (done == received) {
                outcome = await OnRelayAsync(await received.ConfigureAwait(false), output, ask, cancellationToken)
                    .ConfigureAwait(false);
                received = link.ReceiveAsync(cancellationToken).AsTask();
            } else if (done == answer) {
                string text = await answer.ConfigureAwait(false);
                answer = null;
                outcome = await OnAnswerAsync(text, output, cancellationToken).ConfigureAwait(false);
            } else {
                outcome = await OnTimeAsync(output, cancellationToken).ConfigureAwait(false);
            }
            if (outcome is int code) {
                return code;
            }
        }
    }

    private TimeSpan UntilNextDeadline() {
        DateTimeOffset?[] deadlines = [
            candidate is null ? start.ExpiresAt : null,
            sessions.NextDeadline,
            answer is null ? null : answerDeadline,
        ];
        DateTimeOffset? next = deadlines.Min();
        if (next is null) {
            return Timeout.InfiniteTimeSpan;
        }
        TimeSpan left = next.Value - time.GetUtcNow();
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    private async Task<int?> OnRelayAsync(
        RelayEvent relayEvent, TextWriter output, Func<string, string> ask, CancellationToken cancellationToken) {
        if (relayEvent is RelayEvent.Disconnected) {
            return await FailAsync(output, messages.PairFailed, disconnectedFailure, cancellationToken).ConfigureAwait(false);
        }
        SessionUpdate update = relayEvent switch {
            RelayEvent.Received(Envelope envelope) =>
                sessions.Receive(envelope, commit => Route(envelope.Sid, commit)),
            RelayEvent.SessionsClosed(IReadOnlyList<string> sids) => sessions.Drop(sids),
            _ => throw new InvalidOperationException($"relay event has no pairing step event={relayEvent}"),
        };
        await SendAsync(update, cancellationToken).ConfigureAwait(false);
        if (secondCommit) {
            return await FailAsync(output, messages.PairFailed, secondCommitFailure, cancellationToken)
                .ConfigureAwait(false);
        }
        if (candidate is not null && !sessions.Contains(candidate)) {
            return await FailAsync(output, messages.PairFailed, candidateFailure, cancellationToken).ConfigureAwait(false);
        }
        foreach (SessionEvent sessionEvent in update.Events.Where(item => item.Sid == candidate)) {
            if (sessionEvent is SessionEvent.Revealed revealed) {
                await ShowWordsAsync(revealed, output, ask).ConfigureAwait(false);
            } else if (sessionEvent is SessionEvent.Confirmed) {
                browserConfirmed = true;
            }
        }
        return browserConfirmed && terminalYes ? await CompleteAsync(output, cancellationToken).ConfigureAwait(false) : null;
    }

    private CommitRoute Route(string sid, HelloSteps.Commit commit) {
        if (paired.Contains(Base64Url.EncodeToString(commit.BrowserStatic))) {
            return CommitRoute.Offer;
        }
        if (commit.PairTag is not byte[] tag || !SessionTable.IsPairTag(start.Code, channel, tag)) {
            return CommitRoute.Ignore;
        }
        if (candidate is not null) {
            secondCommit = true;
            return CommitRoute.Abort;
        }
        candidate = sid;
        candidateKey = commit.BrowserStatic;
        return CommitRoute.Hold;
    }

    private async Task ShowWordsAsync(SessionEvent.Revealed revealed, TextWriter output, Func<string, string> ask) {
        agent = revealed.Agent;
        string wantsToPair = agent.Length > 0 ? messages.PairWantsToPair(agent) : messages.PairBrowserWantsToPair;
        string[] lines = [wantsToPair, messages.PairWebsiteWords, indent + string.Join(wordSeparator, revealed.Words)];
        foreach (string line in lines) {
            await output.WriteLineAsync(line).ConfigureAwait(false);
        }
        answerDeadline = time.GetUtcNow() + answerTimeout;
        answer = Task.Run(() => ask(messages.PairWordsMatch));
    }

    private async Task<int?> OnAnswerAsync(string text, TextWriter output, CancellationToken cancellationToken) {
        if (!messages.PairYesAnswers.Contains(text.Trim(), StringComparer.OrdinalIgnoreCase)) {
            return await FailAsync(output, messages.PairNotPaired, terminalNoFailure, cancellationToken)
                .ConfigureAwait(false);
        }
        terminalYes = true;
        return browserConfirmed ? await CompleteAsync(output, cancellationToken).ConfigureAwait(false) : null;
    }

    private async Task<int?> OnTimeAsync(TextWriter output, CancellationToken cancellationToken) {
        SessionUpdate expired = sessions.Expire();
        await SendAsync(expired, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = time.GetUtcNow();
        if (candidate is null) {
            return now >= start.ExpiresAt
                ? await FailAsync(output, messages.PairCodeExpired, expiredFailure, cancellationToken).ConfigureAwait(false)
                : null;
        }
        if (!sessions.Contains(candidate)) {
            return await FailAsync(output, messages.PairFailed, candidateFailure, cancellationToken).ConfigureAwait(false);
        }
        return answer is not null && now >= answerDeadline
            ? await FailAsync(output, messages.PairFailed, terminalTimeoutFailure, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private async Task<int> CompleteAsync(TextWriter output, CancellationToken cancellationToken) {
        string sid = candidate!;
        DateTimeOffset now = time.GetUtcNow();
        link.AddDevice(candidateKey, agent.Length > 0 ? agent : browserFallback, now);
        sessions.Establish(sid);
        await link.SendAsync(sessions.Seal(sid, PayloadCodec.Encode(StatusFrame(now))), cancellationToken).ConfigureAwait(false);
        LogEvents.PairingPaired(logger);
        string shown = agent.Length > 0 ? agent : messages.PairBrowserFallback;
        await output.WriteLineAsync(messages.PairPaired(shown)).ConfigureAwait(false);
        return CliExitCodes.Ok;
    }

    private Status StatusFrame(DateTimeOffset now) {
        string shownMachine = SessionTable.CleanName(machine);
        return new Status(
            AppVersion.Text,
            ContractLimits.ContractVersion,
            shownMachine.Length > 0 ? shownMachine : machineFallback,
            StoredTime.Format(now),
            LastRun: null,
            NextRunAt: null,
            Running: null,
            BudgetPaused: false);
    }

    private async Task<int> FailAsync(TextWriter output, string line, string failure, CancellationToken cancellationToken) {
        if (candidate is not null) {
            await SendAsync(sessions.Abort(candidate), cancellationToken).ConfigureAwait(false);
        }
        LogEvents.PairingFailed(logger, failure);
        await output.WriteLineAsync(line).ConfigureAwait(false);
        return CliExitCodes.Failure;
    }

    private async Task SendAsync(SessionUpdate update, CancellationToken cancellationToken) {
        foreach (Envelope reply in update.Replies) {
            await link.SendAsync(reply, cancellationToken).ConfigureAwait(false);
        }
    }
}
