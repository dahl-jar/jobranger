using Jobranger.Relay.Core.Limits;
using Jobranger.Relay.Hubs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jobranger.Relay.Tests.Hubs;

public sealed class HubTransportTests(RelayFactory factory) : IClassFixture<RelayFactory> {
    [Fact]
    public void ShouldBoundHubPipeBuffers() {
        _ = factory.Server;
        HttpConnectionDispatcherOptions options = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<HttpConnectionDispatcherOptions>())
            .OfType<HttpConnectionDispatcherOptions>()
            .Single();

        Assert.Equal(
            (RelayLimits.HubApplicationBufferBytes, RelayLimits.HubTransportBufferBytes, HttpTransportType.WebSockets),
            (options.ApplicationMaxBufferSize, options.TransportMaxBufferSize, options.Transports));
    }

    [Fact]
    public void ShouldBoundServerConnectionBuffers() {
        KestrelServerLimits limits = factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value.Limits;
        SocketTransportOptions sockets = factory.Services.GetRequiredService<IOptions<SocketTransportOptions>>().Value;

        Assert.Equal(
            (RelayLimits.KestrelInputBufferBytes, RelayLimits.KestrelOutputBufferBytes,
             RelayLimits.KestrelInputBufferBytes, RelayLimits.KestrelOutputBufferBytes),
            (limits.MaxRequestBufferSize, limits.MaxResponseBufferSize, sockets.MaxReadBufferSize, sockets.MaxWriteBufferSize));
    }

    [Fact]
    public void ShouldBoundKernelSocketBuffers() {
        SocketTransportOptions sockets = factory.Services.GetRequiredService<IOptions<SocketTransportOptions>>().Value;
        using System.Net.Sockets.Socket listener = sockets.CreateBoundListenSocket(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));

        Assert.All(
            [listener.ReceiveBufferSize, listener.SendBufferSize],
            size => Assert.InRange(size, RelayLimits.KernelSocketBufferBytes, RelayLimits.KernelSocketBufferBytes * 2));
    }

    [Fact]
    public void ShouldCapHubSocketsAtDerivedLimit() {
        HubSocketGate gate = factory.Services.GetRequiredService<HubSocketGate>();
        string[] networks = [.. Enumerable.Range(0, RelayLimits.MaxHubSockets + 1).Select(network => $"network-{network}")];
        bool[] entered = [.. networks.Select(gate.TryEnter)];

        foreach ((string network, bool admitted) in networks.Zip(entered).Where(pair => pair.Second)) {
            gate.Exit(network);
        }

        Assert.Equal((RelayLimits.MaxHubSockets, false), (entered.Count(admitted => admitted), entered[^1]));
    }

    [Fact]
    public void ShouldNotRegisterDataProtection() {
        IDataProtectionProvider? protection = factory.Services.GetService<IDataProtectionProvider>();

        Assert.Null(protection);
    }
}
