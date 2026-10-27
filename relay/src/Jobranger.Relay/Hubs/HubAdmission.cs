using System.Globalization;
using Jobranger.Relay.Core;
using Jobranger.Relay.Pairing;
using Jobranger.Relay.Security;
using Microsoft.AspNetCore.SignalR;

namespace Jobranger.Relay.Hubs;

internal static class HubAdmission {
    public static async Task Check(HttpContext http, RequestDelegate next) {
        if (http.GetEndpoint()?.Metadata.GetMetadata<HubMetadata>() is null || http.User.Claims() is not { } claims) {
            await next(http);
            return;
        }

        HubSocketGate gate = http.RequestServices.GetRequiredService<HubSocketGate>();
        string network = ClientPartition.Network(http.Connection.RemoteIpAddress);
        if (!gate.TryEnter(network)) {
            http.Response.Headers.RetryAfter = ((int)RelayLimits.HubFullRetryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            await RelayProblems.RelayFull.ToResult().ExecuteAsync(http);
            return;
        }

        try {
            if (http.RequestServices.GetRequiredService<ChannelRegistry>().Admits(claims.Channel, claims.Role, claims.Grant)
                    != JoinOutcome.Joined
                && FailureCharge.Charge(http) is { } refused) {
                await refused.ExecuteAsync(http);
                return;
            }

            await next(http);
        } finally {
            gate.Exit(network);
        }
    }
}
