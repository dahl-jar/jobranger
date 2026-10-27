using System.Net;
using System.Net.Sockets;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class LoopbackOnlyHandler : DelegatingHandler {
    private const string localhost = "localhost";

    public LoopbackOnlyHandler() : base(new SocketsHttpHandler {
        AllowAutoRedirect = false,
        UseProxy = false,
        ConnectCallback = ConnectLoopbackAsync,
    }) {
    }

    private static async ValueTask<Stream> ConnectLoopbackAsync(
        SocketsHttpConnectionContext context, CancellationToken cancellationToken) {
        DnsEndPoint endpoint = context.DnsEndPoint;
        IPAddress[] addresses = await ResolveLoopbackAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
        Socket socket = new(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try {
            await socket.ConnectAsync(addresses, endpoint.Port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        } catch {
            socket.Dispose();
            throw;
        }
    }

    private static async Task<IPAddress[]> ResolveLoopbackAsync(string host, CancellationToken cancellationToken) {
        IPAddress[] addresses = IPAddress.TryParse(host, out IPAddress? literal) ? [literal]
            : string.Equals(host, localhost, StringComparison.OrdinalIgnoreCase)
                ? await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false)
                : [];
        if (addresses.Length == 0 || !addresses.All(IPAddress.IsLoopback)) {
            throw new InvalidOperationException($"Tests may only connect to loopback hosts, got host={host}");
        }
        return addresses;
    }
}
