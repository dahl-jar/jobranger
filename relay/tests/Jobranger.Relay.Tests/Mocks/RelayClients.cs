using System.Security.Cryptography;
using Jobranger.Contracts.Channels;
using Jobranger.Relay.Core.Tokens;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Jobranger.Relay.Tests.Mocks;

internal static class RelayClients {
    private const string hubPath = "hub";
    private const string tokenQuery = "access_token";
    private const int channelKeyBytes = 32;

    private static readonly TimeSpan handleLifetime = TimeSpan.FromMinutes(5);

    public static ChannelId NewChannel() => ChannelId.FromPublicKey(RandomNumberGenerator.GetBytes(channelKeyBytes));

    public static string Handle(RelayFactory factory, ChannelId channel, RelayRole role) {
        ChannelHandle handles = factory.Services.GetRequiredService<ChannelHandle>();
        DateTimeOffset expires = factory.Time.GetUtcNow() + handleLifetime;
        return role == RelayRole.Web ? handles.SignWeb(channel, expires, ChannelHandle.NewGrant()) : handles.SignCli(channel, expires);
    }

    public static HubPeer Build(
        RelayFactory factory, string? handle, string? origin, string? address = null, TimeSpan? keepAlive = null) {
        TestServer server = factory.Server;
        string query = handle is null ? string.Empty : $"?{tokenQuery}={Uri.EscapeDataString(handle)}";
        string client = address ?? Addresses.Next();
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, hubPath + query), options => {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.WebSocketFactory = async (context, cancellation) => {
                    WebSocketClient sockets = server.CreateWebSocketClient();
                    sockets.ConfigureRequest = request => {
                        request.Headers[RelayApi.ClientHeader] = client;
                        if (origin is not null) {
                            request.Headers.Origin = origin;
                        }
                    };
                    return await sockets.ConnectAsync(context.Uri, cancellation);
                };
            })
            .AddJsonProtocol(json => json.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, HubTestJsonContext.Default))
            .Build();
        if (keepAlive is { } interval) {
            connection.KeepAliveInterval = interval;
            connection.ServerTimeout = interval * 2;
        }

        return new HubPeer(connection);
    }

    public static async Task<HubPeer> ConnectAsync(
        RelayFactory factory, string handle, string? origin, string? address = null, TimeSpan? keepAlive = null) {
        HubPeer peer = Build(factory, handle, origin, address, keepAlive);
        await peer.Connection.StartAsync(TestContext.Current.CancellationToken);
        await peer.Next(HubPeer.Presence);
        return peer;
    }

    public static Task<HubPeer> ConnectCliAsync(RelayFactory factory, ChannelId channel, string? address = null) =>
        ConnectAsync(factory, Handle(factory, channel, RelayRole.Cli), null, address);

    public static Task<HubPeer> ConnectWebAsync(RelayFactory factory, ChannelId channel, TimeSpan? keepAlive = null) =>
        ConnectAsync(factory, Handle(factory, channel, RelayRole.Web), RelayFactory.SiteOrigin, keepAlive: keepAlive);
}
