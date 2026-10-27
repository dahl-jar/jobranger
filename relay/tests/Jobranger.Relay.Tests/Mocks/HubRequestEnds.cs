using System.Net;
using System.Threading.Channels;
using Jobranger.Relay.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Jobranger.Relay.Tests.Mocks;

internal sealed class HubRequestEnds : IStartupFilter {
    private static readonly TimeSpan wait = TimeSpan.FromSeconds(10);

    private readonly Channel<string> networks = Channel.CreateUnbounded<string>();

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app => {
        app.Use(async (context, nextMiddleware) => {
            try {
                await nextMiddleware(context);
            } finally {
                if (context.Request.Path.StartsWithSegments(HandleAuthHandler.HubPath, StringComparison.Ordinal)) {
                    networks.Writer.TryWrite(ClientPartition.Network(context.Connection.RemoteIpAddress));
                }
            }
        });
        next(app);
    };

    public Task Ended(string address) =>
        EndedIn(ClientPartition.Network(IPAddress.Parse(address))).WaitAsync(wait, TestContext.Current.CancellationToken);

    private async Task EndedIn(string network) {
        string ended;
        do {
            ended = await networks.Reader.ReadAsync(TestContext.Current.CancellationToken);
        } while (!string.Equals(ended, network, StringComparison.Ordinal));
    }
}
