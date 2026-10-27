using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.RateLimiting;
using Jobranger.Contracts.Envelopes;
using Jobranger.Relay.Core.Channels;
using Jobranger.Relay.Core.Limits;
using Jobranger.Relay.Core.Tokens;
using Jobranger.Relay.Security;
using Jobranger.Relay.Telemetry;
using Microsoft.AspNetCore.SignalR;

namespace Jobranger.Relay.Hubs;

internal sealed class EnvelopeFilter(
    ChannelRegistry registry, TimeProvider time, RefusalSampler sampler, RelayMetrics metrics, ILoggerFactory loggers) {
    private const string versionProperty = "v";
    private const string samplerPrefix = "envelope:";
    private const int supportedVersion = 1;

    private static readonly object limiterKey = new();

    public void Open(HubCallerContext caller) =>
        caller.Items[limiterKey] = new BucketLimiter(
            RelayLimits.MessagesBurst, RelayLimits.MessagesPerSecond, RelayLimits.HubRefillPeriod, time);

    public static void Close(HubCallerContext caller) {
        if (caller.Items.Remove(limiterKey, out object? limiter) && limiter is IDisposable disposable) {
            disposable.Dispose();
        }
    }

    public Envelope Accept(HubCallerContext caller, HandleClaims claims, JsonElement raw) {
        if (!TakeMessage(caller)) {
            throw Refuse(claims, HubErrors.RateLimited);
        }

        if (!EnvelopeRules.TryParse(JsonMarshal.GetRawUtf8Value(raw), out Envelope? envelope)) {
            throw Refuse(claims, HasOtherVersion(raw) ? HubErrors.UnsupportedVersion : HubErrors.InvalidEnvelope);
        }

        if (!string.Equals(envelope.Channel, claims.Channel.Value, StringComparison.Ordinal)) {
            throw Refuse(claims, HubErrors.ChannelMismatch);
        }

        if (claims.Role == RelayRole.Web
            && SidRefusal(registry.BindSid(claims.Channel, caller.ConnectionId, envelope.Sid)) is { } refusal) {
            throw Refuse(claims, refusal);
        }

        if (!registry.TryConsumeBytes(claims.Channel, claims.Role, envelope.Ciphertext.Length)) {
            throw Refuse(claims, HubErrors.RateLimited);
        }

        return envelope;
    }

    private static bool TakeMessage(HubCallerContext caller) {
        if (!caller.Items.TryGetValue(limiterKey, out object? value) || value is not RateLimiter limiter) {
            return false;
        }

        using RateLimitLease lease = limiter.AttemptAcquire();
        return lease.IsAcquired;
    }

    private static bool HasOtherVersion(JsonElement raw) =>
        raw.ValueKind == JsonValueKind.Object
        && raw.TryGetProperty(versionProperty, out JsonElement version)
        && version.ValueKind == JsonValueKind.Number
        && version.TryGetInt64(out long major)
        && major != supportedVersion;

    private static string? SidRefusal(SidOutcome outcome) => outcome switch {
        SidOutcome.Bound => null,
        SidOutcome.TooMany => HubErrors.TooManySessions,
        _ => HubErrors.SidInUse,
    };

    private HubException Refuse(HandleClaims claims, string reason) {
        metrics.EnvelopeRejected(reason);
        if (sampler.ShouldLog(samplerPrefix + reason)) {
            RelayLog.For(loggers).EnvelopeRejected(claims.Channel.Value, reason);
        }

        return new HubException(reason);
    }
}
