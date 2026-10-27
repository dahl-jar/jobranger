using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class LoopbackServer : IDisposable {
    private const string headerEnd = "\r\n\r\n";
    private const int bufferBytes = 4096;

    private readonly TcpListener listener = new(IPAddress.Loopback, 0);

    public LoopbackServer(string response, CancellationToken cancellationToken) {
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = AnswerOnceAsync(response, cancellationToken);
    }

    public int Port { get; }

    public Uri AddressFor(string host) => new($"http://{host}:{Port}/");

    public void Dispose() {
        listener.Stop();
        listener.Dispose();
    }

    private async Task AnswerOnceAsync(string response, CancellationToken cancellationToken) {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        using NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[bufferBytes];
        StringBuilder request = new();
        while (!request.ToString().Contains(headerEnd, StringComparison.Ordinal)) {
            int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) {
                return;
            }
            request.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken).ConfigureAwait(false);
    }
}
