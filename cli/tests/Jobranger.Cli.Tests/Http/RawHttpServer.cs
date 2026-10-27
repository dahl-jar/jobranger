using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Jobranger.Cli.Tests.Http;

internal sealed class RawHttpServer : IAsyncDisposable {
    private const string headerEnd = "\r\n\r\n";
    private const int bufferBytes = 4096;

    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly TaskCompletionSource<string> request = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource stop = new();
    private readonly Task serving;

    public RawHttpServer(string response) {
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        serving = ServeAsync(response, stop.Token);
    }

    public int Port { get; }

    public Task<string> Request => request.Task;

    public Uri Url(string path) => new($"http://127.0.0.1:{Port}{path}");

    public async ValueTask DisposeAsync() {
        await stop.CancelAsync().ConfigureAwait(false);
        listener.Stop();
        try {
            await serving.ConfigureAwait(false);
        } catch (Exception error) when (error is OperationCanceledException or SocketException or IOException) {
            request.TrySetException(error);
        }
        listener.Dispose();
        stop.Dispose();
    }

    private async Task ServeAsync(string response, CancellationToken cancellationToken) {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        using NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[bufferBytes];
        StringBuilder text = new();
        while (!text.ToString().Contains(headerEnd, StringComparison.Ordinal)) {
            int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) {
                return;
            }
            text.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }
        request.TrySetResult(text.ToString());
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken).ConfigureAwait(false);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
    }
}
