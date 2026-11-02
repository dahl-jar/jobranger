using System.Net;
using System.Net.Sockets;
using Jobranger.Cli.Config;

namespace Jobranger.Cli.Features.Delivery;

internal sealed class SmtpSocket(DeliveryTransport transport, TimeProvider time) {
    public async Task<Socket?> OpenAsync(SmtpServer server, CancellationToken token) {
        IPAddress[]? found = await ResolveAsync(server.Host, token).ConfigureAwait(false);
        foreach (IPAddress address in found ?? []) {
            if (await TryConnectAsync(address, server.Port, token).ConfigureAwait(false) is Socket socket) {
                return socket;
            }
        }
        return null;
    }

    private async Task<IPAddress[]?> ResolveAsync(string host, CancellationToken token) {
        if (IPAddress.TryParse(host, out IPAddress? literal)) {
            return [literal];
        }
        using CancellationTokenSource timer = new(SendLimits.Operation, time);
        using CancellationTokenSource limited = CancellationTokenSource.CreateLinkedTokenSource(token, timer.Token);
        try {
            return await transport.Resolve(host, limited.Token).WaitAsync(limited.Token).ConfigureAwait(false);
        } catch (Exception error) when (!token.IsCancellationRequested && error is OperationCanceledException or SocketException) {
            return null;
        }
    }

    private async Task<Socket?> TryConnectAsync(IPAddress address, int port, CancellationToken token) {
        using CancellationTokenSource timer = new(SendLimits.Operation, time);
        using CancellationTokenSource limited = CancellationTokenSource.CreateLinkedTokenSource(token, timer.Token);
        Socket? socket = null;
        try {
            socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            await socket.ConnectAsync(new IPEndPoint(address, port), limited.Token).ConfigureAwait(false);
            return socket;
        } catch (Exception error) when (!token.IsCancellationRequested && error is OperationCanceledException or SocketException) {
            socket?.Dispose();
            return null;
        } catch {
            socket?.Dispose();
            throw;
        }
    }
}
