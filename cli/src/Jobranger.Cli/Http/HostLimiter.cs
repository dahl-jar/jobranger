using System.Collections.Frozen;

namespace Jobranger.Cli.Http;

internal sealed class HostLimiter {
    private static readonly Rule fallback = new(TimeSpan.FromSeconds(1), 2);

    private static readonly FrozenDictionary<string, Rule> rules = new Dictionary<string, Rule> {
        ["www.finn.no"] = new(TimeSpan.FromSeconds(2), 1),
        ["www.linkedin.com"] = new(TimeSpan.FromSeconds(3), 1),
        ["pam-stilling-feed.nav.no"] = new(TimeSpan.FromMilliseconds(500), 4),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly TimeProvider time;
    private readonly long origin;
    private readonly Dictionary<string, TimeSpan> arrivals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock gate = new();

    public HostLimiter(TimeProvider time) {
        this.time = time;
        origin = time.GetTimestamp();
    }

    public async Task AcquireAsync(string host, CancellationToken cancellationToken) {
        Rule rule = RuleFor(host);
        TimeSpan wait;
        lock (gate) {
            TimeSpan now = Now();
            TimeSpan arrival = Later(arrivals.GetValueOrDefault(host, now), now);
            wait = Later(arrival - rule.Tolerance - now, TimeSpan.Zero);
            arrivals[host] = arrival + rule.Interval;
        }
        if (wait > TimeSpan.Zero) {
            await Task.Delay(wait, time, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Penalize(string host, TimeSpan retryAfter) {
        Rule rule = RuleFor(host);
        lock (gate) {
            TimeSpan pushed = Now() + retryAfter + rule.Tolerance;
            arrivals[host] = Later(arrivals.GetValueOrDefault(host, pushed), pushed);
        }
    }

    private static Rule RuleFor(string host) => rules.GetValueOrDefault(host, fallback);

    private static TimeSpan Later(TimeSpan left, TimeSpan right) => left > right ? left : right;

    private TimeSpan Now() => time.GetElapsedTime(origin);

    private readonly record struct Rule(TimeSpan Interval, int Burst) {
        public TimeSpan Tolerance => Interval * (Burst - 1);
    }
}
