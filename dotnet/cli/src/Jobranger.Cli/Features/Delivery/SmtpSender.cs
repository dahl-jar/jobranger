using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Jobranger.Cli.Features.Delivery;

internal sealed class SmtpSender(DeliveryTransport transport, IMessages messages, TimeProvider time) {
    private const string localHello = "127.0.0.1";
    private const string canceledMessage = "the caller canceled the email send";

    public async Task<SendFailure?> SendAsync(
        string url,
        string? user,
        string? password,
        RenderedDigest rendered,
        EmailAddresses addresses,
        CancellationToken cancellationToken) {
        if (SmtpServer.Parse(url) is not SmtpServer server) {
            return new SendFailure(SendFailureKind.InvalidSmtpUrl, messages.EmailInvalidSmtpUrl);
        }
        if (LoginFailure(user, password) is SendFailure refused) {
            return refused;
        }
        MimeMessage message;
        try {
            message = SmtpMessage.Build(rendered, addresses, time.GetLocalNow());
        } catch (ParseException error) {
            return new SendFailure(SendFailureKind.UnusableAddress, messages.EmailUnusableAddress, error.GetType().Name);
        }
        using (message) {
            return await SendMessageAsync(server, user, password, message, addresses, cancellationToken).ConfigureAwait(false);
        }
    }

    private static SmtpClient NewClient(SmtpReplyLimit limit, RemoteCertificateValidationCallback isTrusted) => new(limit) {
        LocalDomain = localHello,
        CheckCertificateRevocation = false,
        ServerCertificateValidationCallback = isTrusted,
    };

    private static SecureSocketOptions Security(SmtpServer server) =>
        server.ImplicitTls ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

    private static async Task TransmitAsync(
        SmtpClient client, string? user, string? password, MimeMessage message, CancellationToken token) {
        if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password)) {
            await client.AuthenticateAsync(user, password, token).ConfigureAwait(false);
        }
        await client.SendAsync(message, token).ConfigureAwait(false);
        await QuitAsync(client, token).ConfigureAwait(false);
    }

    private static async Task QuitAsync(SmtpClient client, CancellationToken token) {
        try {
            await client.DisconnectAsync(true, token).ConfigureAwait(false);
        } catch (Exception error) when (SmtpFailures.IsSendError(error)) {
            return;
        }
    }

    private SendFailure? LoginFailure(string? user, string? password) {
        if (string.IsNullOrEmpty(user)) {
            return null;
        }
        if (string.IsNullOrEmpty(password)) {
            return new SendFailure(SendFailureKind.MissingSmtpPassword, messages.EmailMissingSmtpPassword);
        }
        return Ascii.IsValid(user) && Ascii.IsValid(password)
            ? null
            : new SendFailure(SendFailureKind.NonAsciiLogin, messages.EmailNonAsciiLogin);
    }

    private async Task<SendFailure?> SendMessageAsync(
        SmtpServer server,
        string? user,
        string? password,
        MimeMessage message,
        EmailAddresses addresses,
        CancellationToken cancellationToken) {
        using CancellationTokenSource deadline = new(SendLimits.Deadline, time);
        using CancellationTokenSource limited = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        using SmtpReplyLimit limit = new();
        TimedStream? timed = null;
        bool certificateRefused = false;
        bool connected = false;
        try {
            if (await OpenSocketAsync(server, limited.Token).ConfigureAwait(false) is not Socket socket) {
                return Unreachable(server);
            }
            timed = new TimedStream(new NetworkStream(socket, ownsSocket: true), SendLimits.Operation, time);
            using SmtpClient client = NewClient(limit, (_, certificate, _, errors) => {
                bool trusted = transport.IsTrusted(certificate, errors);
                certificateRefused |= !trusted;
                return trusted;
            });
            await client.ConnectAsync(timed, server.Host, server.Port, Security(server), limited.Token).ConfigureAwait(false);
            connected = true;
            await TransmitAsync(client, user, password, message, limited.Token).ConfigureAwait(false);
            return null;
        } catch (Exception error) when (cancellationToken.IsCancellationRequested) {
            throw new OperationCanceledException(canceledMessage, error, cancellationToken);
        } catch (Exception error) when (SmtpFailures.IsSendError(error)) {
            SmtpSendState state = new(
                server,
                addresses,
                deadline.IsCancellationRequested,
                limit.IsOver,
                limit.SawNonReply,
                timed?.TimedOut ?? false,
                certificateRefused,
                connected);
            return SmtpFailures.Of(error, state, messages);
        } finally {
            timed?.Dispose();
        }
    }

    private async Task<Socket?> OpenSocketAsync(SmtpServer server, CancellationToken token) {
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

    private SendFailure Unreachable(SmtpServer server) =>
        new(SendFailureKind.SmtpUnreachable, messages.EmailSmtpUnreachable(server.Host, server.Port));
}
