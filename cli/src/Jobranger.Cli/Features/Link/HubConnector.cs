using System.Globalization;
using System.Net.WebSockets;
using Jobranger.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Jobranger.Cli.Features.Link;

internal sealed class HubConnector(Uri relay, TimeProvider time, TimeSpan connectTimeout) : IRelayConnector {
    private const string hubRoute = "hub";
    private const string tokenQuery = "access_token";
    private const string retryAfterHeader = "Retry-After";
    private const int sendBytesPerSecond = 192 * 1024;
    private const char slash = '/';

    private static readonly TimeSpan defaultConnectTimeout = TimeSpan.FromSeconds(15);

    public HubConnector(Uri relay, TimeProvider time)
        : this(relay, time, defaultConnectTimeout) {
    }

    public async Task<IRelayConnection> ConnectAsync(string handle, CancellationToken cancellationToken) {
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(HubUrl(handle), HttpTransportType.WebSockets, options => {
                options.SkipNegotiation = true;
                options.WebSocketFactory = OpenSocketAsync;
            })
            .AddJsonProtocol(json => {
                json.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, ContractsJsonContext.Default);
                json.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, LinkJsonContext.Default);
            })
            .Build();
        connection.HandshakeTimeout = connectTimeout;
        HubRelayConnection hub = new(connection, new SendPacer(time, sendBytesPerSecond), time);
        using CancellationTokenSource deadline = new(connectTimeout, time);
        using CancellationTokenSource connecting =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try {
            await hub.StartAsync(connecting.Token).ConfigureAwait(false);
            return hub;
        } catch (Exception error) {
            await hub.DisposeAsync().ConfigureAwait(false);
            if (error is RelayUnavailableException
                || (error is OperationCanceledException && cancellationToken.IsCancellationRequested)) {
                throw;
            }
            throw new RelayUnavailableException("relay hub connection failed");
        }
    }

    private static async ValueTask<WebSocket> OpenSocketAsync(
        WebSocketConnectionContext context, CancellationToken cancellationToken) {
        ClientWebSocket socket = new();
        socket.Options.CollectHttpResponseDetails = true;
        try {
            await socket.ConnectAsync(context.Uri, cancellationToken).ConfigureAwait(false);
            return socket;
        } catch (WebSocketException) {
            TimeSpan retryAfter = RetryAfter(socket);
            socket.Dispose();
            throw new RelayUnavailableException("relay hub connection failed", retryAfter);
        } catch {
            socket.Dispose();
            throw;
        }
    }

    private static TimeSpan RetryAfter(ClientWebSocket socket) =>
        socket.HttpResponseHeaders?.TryGetValue(retryAfterHeader, out IEnumerable<string>? values) == true
        && int.TryParse(values.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out int seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;

    private Uri HubUrl(string handle) =>
        new($"{relay.AbsoluteUri.TrimEnd(slash)}{slash}{hubRoute}?{tokenQuery}={Uri.EscapeDataString(handle)}");
}
