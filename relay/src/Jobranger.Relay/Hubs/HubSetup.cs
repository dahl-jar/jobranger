using System.Net;
using System.Net.Sockets;
using Jobranger.Contracts;
using Jobranger.Contracts.Channels;
using Jobranger.Relay.Core;
using Jobranger.Relay.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jobranger.Relay.Hubs;

internal static class HubSetup {
    private const string hubPolicy = "relay-handle";
    private const string dispatcherCategory = "Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcher";
    private const string connectionHandlerCategory = "Microsoft.AspNetCore.SignalR.HubConnectionHandler";

    public static WebApplicationBuilder AddRelayHub(this WebApplicationBuilder builder) {
        builder.WebHost.ConfigureKestrel(kestrel => {
            kestrel.Limits.MaxRequestBufferSize = RelayLimits.KestrelInputBufferBytes;
            kestrel.Limits.MaxResponseBufferSize = RelayLimits.KestrelOutputBufferBytes;
        });
        builder.WebHost.UseSockets(sockets => {
            sockets.MaxReadBufferSize = RelayLimits.KestrelInputBufferBytes;
            sockets.MaxWriteBufferSize = RelayLimits.KestrelOutputBufferBytes;
            sockets.CreateBoundListenSocket = BoundedListenSocket;
        });
        builder.Logging.AddFilter(dispatcherCategory, LogLevel.None);
        builder.Logging.AddFilter(connectionHandlerCategory, LogLevel.None);

        IServiceCollection services = builder.Services;
        services.AddSingleton<ChannelRegistry>();
        services.AddSingleton<EnvelopeFilter>();
        services.AddSingleton<HubConnections>();
        services.AddSingleton<HubLifetimeManager<RelayHub>, RelayLifetimeManager>();
        services.TryAddSingleton(new HubSocketGate(RelayLimits.MaxHubSockets, RelayLimits.MaxHubSocketsPerNetwork));
        services.AddSingleton<HubDelivery>();
        services.AddSingleton<HubIdleWatch>();
        services.AddSingleton<HubErrorFilter>();
        services.AddWebEncoders();
        services.AddAuthenticationCore(authentication => authentication.DefaultScheme = HandleAuthHandler.SchemeName);
        new AuthenticationBuilder(services)
            .AddScheme<AuthenticationSchemeOptions, HandleAuthHandler>(HandleAuthHandler.SchemeName, null);
        services.AddAuthorizationBuilder()
            .AddPolicy(hubPolicy, policy => policy.RequireClaim(HubClaims.ChannelType).RequireClaim(HubClaims.RoleType));
        services.AddSignalR(hub => {
            hub.MaximumReceiveMessageSize = RelayLimits.MaxFrameBytes;
            hub.KeepAliveInterval = RelayLimits.HubKeepAlive;
            hub.ClientTimeoutInterval = RelayLimits.HubClientTimeout;
            hub.HandshakeTimeout = RelayLimits.HubHandshakeTimeout;
            hub.MaximumParallelInvocationsPerClient = 1;
            hub.EnableDetailedErrors = false;
            hub.AddFilter<HubErrorFilter>();
        }).AddJsonProtocol(json => {
            json.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, RelayJsonContext.Default);
            json.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, ContractsJsonContext.Default);
        });
        return builder;
    }

    public static Task HideNegotiate(HttpContext http, RequestDelegate next) {
        if (http.GetEndpoint()?.Metadata.GetMetadata<NegotiateMetadata>() is null) {
            return next(http);
        }

        http.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    public static WebApplication MapRelayHub(this WebApplication app) {
        HubIdleWatch idle = app.Services.GetRequiredService<HubIdleWatch>();
        HttpConnectionDispatcherOptions options = new() {
            Transports = HttpTransportType.WebSockets,
            ApplicationMaxBufferSize = RelayLimits.HubApplicationBufferBytes,
            TransportMaxBufferSize = RelayLimits.HubTransportBufferBytes,
        };
        app.MapConnections(HandleAuthHandler.HubPath, options, connection => {
            connection.Use(idle.Wrap);
            connection.UseHub<RelayHub>();
        })
            .WithMetadata(new HubMetadata(typeof(RelayHub)))
            .RequireAuthorization(hubPolicy)
            .RequireRelayLimit(RateLimiting.HubSockets)
            .RequireFailureLimit(RateLimiting.Session);
        return app;
    }

    private static Socket BoundedListenSocket(EndPoint endpoint) {
        Socket listener = SocketTransportOptions.CreateDefaultBoundListenSocket(endpoint);
        listener.ReceiveBufferSize = RelayLimits.KernelSocketBufferBytes;
        listener.SendBufferSize = RelayLimits.KernelSocketBufferBytes;
        return listener;
    }
}
