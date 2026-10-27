using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Jobranger.Relay.Tests.Mocks;

internal sealed class PeerAddressFilter : IStartupFilter {
    public const string PeerHeader = "X-Test-Peer";

    private const string clusterPeer = "10.42.0.9";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app => {
        app.Use((context, nextMiddleware) => {
            string peer = context.Request.Headers[PeerHeader].FirstOrDefault() ?? clusterPeer;
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            return nextMiddleware(context);
        });
        next(app);
    };
}
