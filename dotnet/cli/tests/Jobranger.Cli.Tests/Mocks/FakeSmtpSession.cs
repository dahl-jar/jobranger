using System.Net.Security;
using System.Text;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeSmtpSession(Stream network, FakeSmtpServer server, CancellationToken cancellationToken)
    : IDisposable {
    private const string dataEnd = ".";
    private const string stuffedDot = "..";
    private const string lineEnd = "\r\n";
    private const int helloBufferBytes = 4096;

    private Stream stream = network;
    private StreamReader reader = Reader(network);

    public async Task SendAsync(string text) {
        await stream.WriteAsync(Encoding.Latin1.GetBytes(text), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public async Task<string> ReadLineAsync() {
        string line = await ReadRawLineAsync();
        server.Commands.Add(line);
        return line;
    }

    public async Task UpgradeAsync() {
        SslStream secure = new(stream, leaveInnerStreamOpen: true);
        await secure.AuthenticateAsServerAsync(
            new SslServerAuthenticationOptions { ServerCertificate = SmtpCertificates.Server }, cancellationToken);
        reader.Dispose();
        stream = secure;
        reader = Reader(secure);
    }

    public async Task ReadDataAsync() {
        StringBuilder data = new();
        string line = await ReadRawLineAsync();
        while (line != dataEnd) {
            data.Append(line.StartsWith(stuffedDot, StringComparison.Ordinal) ? line[1..] : line).Append(lineEnd);
            line = await ReadRawLineAsync();
        }
        server.Messages.Add(Encoding.Latin1.GetBytes(data.ToString()));
    }

    public async Task ReadClientHelloAsync() {
        byte[] hello = new byte[helloBufferBytes];
        await stream.ReadAtLeastAsync(hello, 1, cancellationToken: cancellationToken);
    }

    public async Task StallAsync() {
        server.MarkWaiting();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    public void Dispose() {
        reader.Dispose();
        stream.Dispose();
    }

    private static StreamReader Reader(Stream source) => new(source, Encoding.Latin1, leaveOpen: true);

    private async Task<string> ReadRawLineAsync() =>
        await reader.ReadLineAsync(cancellationToken) ?? throw new EndOfStreamException("smtp client closed the connection");
}
