using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeSmtpServer : IAsyncDisposable {
    private const string startTls = "STARTTLS";
    private const string greeting = "220 fake ready\r\n";
    private const string startTlsReady = "220 go\r\n";
    private const string okLine = "250 ok\r\n";
    private const string dataReady = "354 send\r\n";
    private const string authOk = "235 ok\r\n";
    private const string goodbye = "221 bye\r\n";

    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly TaskCompletionSource waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task serving;

    public FakeSmtpServer(Func<FakeSmtpSession, Task> script) {
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        serving = ServeAsync(script, stop.Token);
    }

    public int Port { get; }

    public List<byte[]> Messages { get; } = [];

    public List<string> Commands { get; } = [];

    public Task Waiting => waiting.Task.WaitAsync(TestWaits.Step);

    public static FakeSmtpServer Accepting(params string[] extensions) => new(async session => {
        await session.SendAsync(greeting);
        await PlayAsync(session, Offered(extensions));
    });

    public static FakeSmtpServer AcceptingTls() => new(async session => {
        await session.UpgradeAsync();
        await session.SendAsync(greeting);
        await PlayAsync(session, []);
    });

    public static FakeSmtpServer SlowlyAccepting(Action beforeAnswer) => new(async session => {
        beforeAnswer();
        await session.SendAsync(greeting);
        await PlayAsync(session, [startTls], beforeAnswer);
    });

    public static FakeSmtpServer AnsweringQuit(Func<FakeSmtpSession, Task> answerQuit) => new(async session => {
        await session.SendAsync(greeting);
        await PlayAsync(session, [startTls], answerQuit: answerQuit);
    });

    public static async Task HelloAsync(FakeSmtpSession session, params string[] extensions) {
        await session.ReadLineAsync();
        await session.SendAsync(HelloReply(extensions));
    }

    public static async Task ReachStartTlsAsync(FakeSmtpSession session, params string[] extensions) {
        await session.SendAsync(greeting);
        await HelloAsync(session, [startTls, .. extensions]);
        await session.ReadLineAsync();
        await session.SendAsync(startTlsReady);
    }

    public string Url(string scheme) => $"{scheme}://127.0.0.1:{Port}";

    public void MarkWaiting() => waiting.TrySetResult();

    public async ValueTask DisposeAsync() {
        await stop.CancelAsync();
        listener.Stop();
        try {
            await serving.WaitAsync(TestWaits.Step);
        } catch (Exception error) when (error
            is OperationCanceledException or IOException or SocketException or ObjectDisposedException or AuthenticationException) {
            waiting.TrySetException(error);
        }
        listener.Dispose();
        stop.Dispose();
    }

    private static string[] Offered(string[] extensions) => extensions.Length == 0 ? [startTls] : extensions;

    private static string HelloReply(IEnumerable<string> extensions) =>
        string.Concat(extensions.Select(extension => $"250-{extension}\r\n")) + okLine;

    private static async Task PlayAsync(
        FakeSmtpSession session, string[] extensions, Action? beforeAnswer = null, Func<FakeSmtpSession, Task>? answerQuit = null) {
        async Task AnswerAsync(string text) {
            beforeAnswer?.Invoke();
            await session.SendAsync(text);
        }
        string[] offered = extensions;
        while (true) {
            string command = await session.ReadLineAsync();
            if (command.StartsWith("EHLO", StringComparison.Ordinal)) {
                await AnswerAsync(HelloReply(offered));
            } else if (command == startTls) {
                await AnswerAsync(startTlsReady);
                await session.UpgradeAsync();
                offered = [.. offered.Where(extension => extension != startTls)];
            } else if (command.StartsWith("AUTH", StringComparison.Ordinal)) {
                await AnswerAsync(authOk);
            } else if (command == "DATA") {
                await AnswerAsync(dataReady);
                await session.ReadDataAsync();
                await AnswerAsync(okLine);
            } else if (command == "QUIT") {
                await (answerQuit?.Invoke(session) ?? AnswerAsync(goodbye));
                return;
            } else {
                await AnswerAsync(okLine);
            }
        }
    }

    private async Task ServeAsync(Func<FakeSmtpSession, Task> script, CancellationToken cancellationToken) {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
        using FakeSmtpSession session = new(client.GetStream(), this, cancellationToken);
        await script(session);
    }
}
