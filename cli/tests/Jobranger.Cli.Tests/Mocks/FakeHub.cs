using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeHub : IAsyncDisposable {
    private const string acceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const string keyHeader = "Sec-WebSocket-Key:";
    private const string headerEnd = "\r\n\r\n";
    private const string handshakeReply = "{}";
    private const string handshakeError = "{\"error\":\"hub refused the handshake\"}";
    private const char recordSeparator = '\u001e';
    private const byte finalTextFrame = 0x81;
    private const byte finalCloseFrame = 0x88;
    private const byte lengthBits = 0x7F;
    private const byte maskBit = 0x80;
    private const int shortLength = 126;
    private const int longLength = 127;
    private const int maskBytes = 4;
    private const int bufferBytes = 1024;

    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly ConcurrentQueue<TcpClient> clients = [];
    private readonly CancellationTokenSource stop = new();
    private readonly HubAnswer answer;
    private readonly int retryAfterSeconds;
    private readonly string[] frames;
    private readonly Task accepting;
    private int framesRead;

    private FakeHub(HubAnswer answer, int retryAfterSeconds, string[] frames) {
        this.answer = answer;
        this.retryAfterSeconds = retryAfterSeconds;
        this.frames = frames;
        listener.Start();
        accepting = AcceptAsync();
    }

    public Uri Address => new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");

    public int FramesRead => Volatile.Read(ref framesRead);

    public static FakeHub Start(HubAnswer answer) => new(answer, 0, []);

    public static FakeHub Busy(int retryAfterSeconds) => new(HubAnswer.Busy, retryAfterSeconds, []);

    public static FakeHub Open(params string[] frames) => new(HubAnswer.Open, 0, frames);

    public static string Invocation(string target, string argument) =>
        $"{{\"type\":1,\"target\":\"{target}\",\"arguments\":[{argument}]}}";

    public void Drop() {
        foreach (TcpClient client in clients) {
            client.Close();
        }
    }

    public async ValueTask DisposeAsync() {
        await stop.CancelAsync();
        await accepting;
        listener.Stop();
        Drop();
        listener.Dispose();
        stop.Dispose();
    }

    private async Task AcceptAsync() {
        try {
            while (true) {
                TcpClient client = await listener.AcceptTcpClientAsync(stop.Token);
                clients.Enqueue(client);
                _ = AnswerAsync(client);
            }
        } catch (Exception error) when (IsShutdown(error)) {
        }
    }

    private async Task AnswerAsync(TcpClient client) {
        try {
            NetworkStream stream = client.GetStream();
            string key = await ReadKeyAsync(stream);
            await AnswerAsync(stream, key);
        } catch (Exception error) when (IsShutdown(error)) {
        }
    }

    private async Task AnswerAsync(NetworkStream stream, string key) {
        switch (answer) {
            case HubAnswer.Busy:
                await WriteAsync(stream, Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 503 Service Unavailable\r\nRetry-After: {retryAfterSeconds}\r\nContent-Length: 0\r\n\r\n"));
                stream.Close();
                return;
            case HubAnswer.NoAnswer:
                await Task.Delay(Timeout.Infinite, stop.Token);
                return;
            case HubAnswer.CloseAfterUpgrade:
                await WriteAsync(stream, [.. Upgrade(key), finalCloseFrame, 0]);
                stream.Close();
                return;
            case HubAnswer.SilentAfterUpgrade:
                await WriteAsync(stream, Upgrade(key));
                await Task.Delay(Timeout.Infinite, stop.Token);
                return;
            case HubAnswer.HandshakeError:
                await WriteAsync(stream, Upgrade(key));
                await ReadFrameAsync(stream);
                await WriteAsync(stream, TextFrame(handshakeError));
                await Task.Delay(Timeout.Infinite, stop.Token);
                return;
            default:
                await WriteAsync(stream, Upgrade(key));
                await ReadFrameAsync(stream);
                await WriteAsync(stream, TextFrame(handshakeReply));
                foreach (string frame in frames) {
                    await WriteAsync(stream, TextFrame(frame));
                }
                while (true) {
                    await ReadFrameAsync(stream);
                    Interlocked.Increment(ref framesRead);
                }
        }
    }

    private async Task<string> ReadKeyAsync(NetworkStream stream) {
        byte[] buffer = new byte[bufferBytes];
        StringBuilder request = new();
        while (!request.ToString().Contains(headerEnd, StringComparison.Ordinal)) {
            int read = await stream.ReadAsync(buffer, stop.Token);
            if (read == 0) {
                throw new IOException("client closed before the upgrade request ended");
            }
            request.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }
        string line = request.ToString().Split("\r\n")
            .Single(header => header.StartsWith(keyHeader, StringComparison.OrdinalIgnoreCase));
        return line[keyHeader.Length..].Trim();
    }

    private async Task ReadFrameAsync(NetworkStream stream) {
        byte[] head = new byte[2];
        await stream.ReadExactlyAsync(head, stop.Token);
        long length = head[1] & lengthBits;
        if (length == shortLength) {
            byte[] extended = new byte[2];
            await stream.ReadExactlyAsync(extended, stop.Token);
            length = (extended[0] << 8) | extended[1];
        } else if (length == longLength) {
            byte[] extended = new byte[8];
            await stream.ReadExactlyAsync(extended, stop.Token);
            length = (long)System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(extended);
        }
        int masked = (head[1] & maskBit) != 0 ? maskBytes : 0;
        await stream.ReadExactlyAsync(new byte[masked + length], stop.Token);
    }

    private Task WriteAsync(NetworkStream stream, byte[] bytes) => stream.WriteAsync(bytes, stop.Token).AsTask();

    [SuppressMessage("Security", "CA5350", Justification = "RFC 6455 defines Sec-WebSocket-Accept as a SHA-1 hash")]
    private static byte[] Upgrade(string key) {
        string accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + acceptGuid)));
        return Encoding.ASCII.GetBytes(
            "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
            + $"Sec-WebSocket-Accept: {accept}\r\n\r\n");
    }

    private static byte[] TextFrame(string message) {
        byte[] payload = Encoding.UTF8.GetBytes(message + recordSeparator);
        byte[] head = payload.Length < shortLength
            ? [finalTextFrame, (byte)payload.Length]
            : [finalTextFrame, shortLength, (byte)(payload.Length >> 8), (byte)payload.Length];
        return [.. head, .. payload];
    }

    private static bool IsShutdown(Exception error) =>
        error is IOException or SocketException or OperationCanceledException or ObjectDisposedException;
}
