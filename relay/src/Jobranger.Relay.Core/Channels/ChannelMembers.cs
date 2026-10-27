using System.Threading.RateLimiting;
using Jobranger.Relay.Core.Limits;
using Jobranger.Relay.Core.Tokens;

namespace Jobranger.Relay.Core.Channels;

internal sealed class ChannelMembers(TimeProvider time) : IDisposable {
    private readonly Dictionary<string, string> browsers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> sidOwners = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> sidsByBrowser = new(StringComparer.Ordinal);
    private readonly BucketLimiter cliBytes = NewByteBucket(time);
    private readonly BucketLimiter browserBytes = NewByteBucket(time);
    private string? cli;

    public bool IsEmpty => cli is null && browsers.Count == 0;

    public Presence Presence => new(cli is not null, browsers.Count);

    public JoinOutcome Admits(RelayRole role, string? grant) {
        if (role == RelayRole.Cli) {
            return JoinOutcome.Joined;
        }

        if (browsers.Count >= RelayLimits.MaxBrowsersPerChannel) {
            return JoinOutcome.ChannelFull;
        }

        return grant is not { } held || SocketsOf(held) >= RelayLimits.MaxSocketsPerTicket ? JoinOutcome.TicketFull : JoinOutcome.Joined;
    }

    public JoinResult Join(RelayRole role, string connectionId, string? grant) {
        if (role == RelayRole.Cli) {
            string? replaced = cli;
            cli = connectionId;
            ReleaseSids();
            return new JoinResult(JoinOutcome.Joined, replaced);
        }

        if (grant is not { } held) {
            return new JoinResult(JoinOutcome.TicketFull, null);
        }

        if (Admits(role, held) is not JoinOutcome.Joined and var refused) {
            return new JoinResult(refused, null);
        }

        browsers.Add(connectionId, held);
        return new JoinResult(JoinOutcome.Joined, null);
    }

    public IReadOnlyList<string> Members(RelayRole role) => role == RelayRole.Cli
        ? cli is { } connection ? [connection] : []
        : [.. browsers.Keys];

    public Departure? Leave(RelayRole role, string connectionId) {
        if (role == RelayRole.Cli) {
            if (!string.Equals(cli, connectionId, StringComparison.Ordinal)) {
                return null;
            }

            cli = null;
            ReleaseSids();
            return new Departure(Presence, []);
        }

        if (!browsers.Remove(connectionId)) {
            return null;
        }

        List<string> released = sidsByBrowser.Remove(connectionId, out List<string>? sids) ? sids : [];
        foreach (string sid in released) {
            sidOwners.Remove(sid);
        }

        return new Departure(Presence, released);
    }

    public SidOutcome Bind(string connectionId, string sid) {
        if (!browsers.ContainsKey(connectionId)) {
            return SidOutcome.Taken;
        }

        if (sidOwners.TryGetValue(sid, out string? owner)) {
            return string.Equals(owner, connectionId, StringComparison.Ordinal) ? SidOutcome.Bound : SidOutcome.Taken;
        }

        if (!sidsByBrowser.TryGetValue(connectionId, out List<string>? held)) {
            held = [];
            sidsByBrowser.Add(connectionId, held);
        }

        if (held.Count >= RelayLimits.MaxSidsPerConnection) {
            return SidOutcome.TooMany;
        }

        held.Add(sid);
        sidOwners.Add(sid, connectionId);
        return SidOutcome.Bound;
    }

    public bool TryConsumeBytes(RelayRole sender, int count) {
        using RateLimitLease lease = (sender == RelayRole.Cli ? cliBytes : browserBytes).AttemptAcquire(count);
        return lease.IsAcquired;
    }

    public void Dispose() {
        cliBytes.Dispose();
        browserBytes.Dispose();
    }

    private void ReleaseSids() {
        sidOwners.Clear();
        sidsByBrowser.Clear();
    }

    private int SocketsOf(string grant) =>
        browsers.Values.Count(held => string.Equals(held, grant, StringComparison.Ordinal));

    private static BucketLimiter NewByteBucket(TimeProvider time) =>
        new(RelayLimits.ChannelBytesBurst, RelayLimits.ChannelBytesPerSecond, RelayLimits.HubRefillPeriod, time);
}
