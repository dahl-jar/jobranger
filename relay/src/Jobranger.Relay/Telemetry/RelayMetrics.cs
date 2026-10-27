using System.Diagnostics.Metrics;
using Jobranger.Relay.Core;

namespace Jobranger.Relay.Telemetry;

internal sealed class RelayMetrics
{
    public const string MeterName = "Jobranger.Relay";

    private const string codeTag = "code";
    private const string policyTag = "policy";
    private const string reasonTag = "reason";

    private readonly Counter<long> pairingsStarted;
    private readonly Counter<long> pairingsClaimed;
    private readonly Counter<long> requestsRejected;
    private readonly Counter<long> rateLimited;
    private readonly Counter<long> envelopesForwarded;
    private readonly Counter<long> envelopeChars;
    private readonly Counter<long> envelopesRejected;
    private readonly Counter<long> connectionsRejected;

    public RelayMetrics(IMeterFactory meters, ChannelRegistry registry)
    {
        Meter meter = meters.Create(MeterName);
        pairingsStarted = meter.CreateCounter<long>("relay.pairings.started");
        pairingsClaimed = meter.CreateCounter<long>("relay.pairings.claimed");
        requestsRejected = meter.CreateCounter<long>("relay.requests.rejected");
        rateLimited = meter.CreateCounter<long>("relay.rate_limited");
        envelopesForwarded = meter.CreateCounter<long>("relay.envelopes.forwarded");
        envelopeChars = meter.CreateCounter<long>("relay.envelopes.chars");
        envelopesRejected = meter.CreateCounter<long>("relay.envelopes.rejected");
        connectionsRejected = meter.CreateCounter<long>("relay.connections.rejected");
        meter.CreateObservableGauge("relay.channels.active", () => registry.ActiveChannels);
    }

    public void PairingStarted() => pairingsStarted.Add(1);

    public void PairingClaimed() => pairingsClaimed.Add(1);

    public void RequestRejected(string code) => CountBy(requestsRejected, codeTag, code);

    public void RateLimited(string policy) => CountBy(rateLimited, policyTag, policy);

    public void EnvelopeForwarded(int chars)
    {
        envelopesForwarded.Add(1);
        envelopeChars.Add(chars);
    }

    public void EnvelopeRejected(string reason) => CountBy(envelopesRejected, reasonTag, reason);

    public void ConnectionRejected(string reason) => CountBy(connectionsRejected, reasonTag, reason);

    private static void CountBy(Counter<long> counter, string tag, string value) =>
        counter.Add(1, new KeyValuePair<string, object?>(tag, value));
}
