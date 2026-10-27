using System.Text.Json;
using Jobranger.Relay.Core;
using Jobranger.Relay.Security;
using Jobranger.Relay.Telemetry;
using Microsoft.AspNetCore.SignalR;

namespace Jobranger.Relay.Hubs;

internal sealed class RelayHub(
    ChannelRegistry registry,
    EnvelopeFilter filter,
    HubConnections connections,
    HubDelivery delivery,
    RefusalSampler sampler,
    RelayMetrics metrics,
    ILoggerFactory loggers) : Hub {
    private const string receiveMethod = "Receive";
    private const string presenceMethod = "Presence";
    private const string sessionsClosedMethod = "SessionsClosed";
    private const string replacedMethod = "Replaced";
    private const string samplerPrefix = "join:";

    public override async Task OnConnectedAsync() {
        HandleClaims claims = Context.Claims();
        JoinResult joined = registry.TryJoin(claims.Channel, claims.Role, Context.ConnectionId, claims.Grant);
        if (joined.Outcome != JoinOutcome.Joined) {
            string reason = HubErrors.For(joined.Outcome);
            metrics.ConnectionRejected(reason);
            if (sampler.ShouldLog(samplerPrefix + reason)) {
                RelayLog.For(loggers).HubConnectionRefused(claims.Channel.Value, reason);
            }

            throw new HubException(reason);
        }

        filter.Open(Context);
        if (joined.Replaced is { } replaced) {
            await delivery.Send(
                registry.Members(claims.Channel, RelayRole.Web),
                presenceMethod,
                [registry.Snapshot(claims.Channel) with { CliOnline = false }]);
            await delivery.Send([replaced], replacedMethod, []);
            connections.Find(replaced)?.Abort();
        }

        await Announce(claims.Channel, registry.Snapshot(claims.Channel));
    }

    public async Task Send(JsonElement envelope) {
        HandleClaims claims = Context.Claims();
        Envelope accepted = filter.Accept(Context, claims, envelope);
        await delivery.Send(registry.Members(claims.Channel, RelayRoles.Other(claims.Role)), receiveMethod, [accepted]);
        metrics.EnvelopeForwarded(accepted.Ciphertext.Length);
    }

    public override async Task OnDisconnectedAsync(Exception? exception) {
        EnvelopeFilter.Close(Context);
        HandleClaims claims = Context.Claims();
        if (registry.Leave(claims.Channel, claims.Role, Context.ConnectionId) is not { } departure) {
            return;
        }

        if (departure.Sids.Count > 0) {
            await delivery.Send(
                registry.Members(claims.Channel, RelayRole.Cli), sessionsClosedMethod, [new SessionsClosed(departure.Sids)]);
        }

        await Announce(claims.Channel, departure.Presence);
    }

    private Task Announce(ChannelId channel, Presence presence) => delivery.Send(
        [.. registry.Members(channel, RelayRole.Cli), .. registry.Members(channel, RelayRole.Web)],
        presenceMethod,
        [presence]);
}
