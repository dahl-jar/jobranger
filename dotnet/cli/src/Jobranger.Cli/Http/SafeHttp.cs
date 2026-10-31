using System.Net;
using System.Net.Sockets;

namespace Jobranger.Cli.Http;

internal static class SafeHttp {
    private const string userAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
        + "(KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36";

    private static readonly TimeSpan readTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan connectTimeout = TimeSpan.FromSeconds(30);

    public static HttpClient CreateSourceClient(HostLimiter limiter, TimeProvider time) =>
        CreateClient(limiter, time, PublicAddress.IsPublic);

    internal static HttpClient CreateClient(HostLimiter limiter, TimeProvider time, Func<IPAddress, bool> isAllowed) {
        SocketsHttpHandler sockets = new() {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = connectTimeout,
            ConnectCallback = (context, cancellationToken) => ConnectAsync(context, time, isAllowed, cancellationToken),
        };
        HttpClient client = new(new HostLimitHandler(limiter, time) { InnerHandler = sockets }) {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return client;
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context, TimeProvider time, Func<IPAddress, bool> isAllowed,
        CancellationToken cancellationToken) {
        DnsEndPoint endpoint = context.DnsEndPoint;
        IPAddress[] addresses = IPAddress.TryParse(endpoint.Host, out IPAddress? literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
        if (addresses.Length == 0 || !addresses.All(isAllowed)) {
            throw new RefusedRequestException($"private address host={endpoint.Host}");
        }
        Socket socket = new(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try {
            await socket.ConnectAsync(addresses, endpoint.Port, cancellationToken).ConfigureAwait(false);
            return new ReadTimeoutStream(new NetworkStream(socket, ownsSocket: true), readTimeout, time);
        } catch {
            socket.Dispose();
            throw;
        }
    }
}
