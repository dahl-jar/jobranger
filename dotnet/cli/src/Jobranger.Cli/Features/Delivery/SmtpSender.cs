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

internal sealed class SmtpSender(
    string? url,
    string? user,
    string? password,
    DeliveryTransport transport,
    IMessages messages,
    TimeProvider time) : IEmailSender {
    private const string localHello = "127.0.0.1";
    private const string canceledMessage = "the caller canceled the email send";
    private const string unreadableChallengeMessage = "the SMTP server sent a login challenge that could not be read";

    public async Task<SendFailure?> SendAsync(
        RenderedDigest rendered,
        EmailAddresses addresses,
        CancellationToken cancellationToken) {
        if (url is null) {
            return new SendFailure(SendFailureKind.MissingSmtpUrl, messages.EmailMissingSmtpUrl);
        }
        if (SmtpServer.Parse(url) is not SmtpServer server) {
            return new SendFailure(SendFailureKind.InvalidSmtpUrl, messages.EmailInvalidSmtpUrl);
        }
        if (LoginFailure() is SendFailure refused) {
            return refused;
        }
        MimeMessage message;
        try {
            message = SmtpMessage.Build(rendered, addresses, time.GetLocalNow());
        } catch (ParseException error) {
            return new SendFailure(SendFailureKind.UnusableAddress, messages.EmailUnusableAddress, error.GetType().Name);
        }
        using (message) {
            return await SendMessageAsync(server, message, addresses, cancellationToken).ConfigureAwait(false);
        }
    }

    private static SmtpClient NewClient(SmtpReplyLimit replyLimit, RemoteCertificateValidationCallback isTrusted) => new(replyLimit) {
        LocalDomain = localHello,
        CheckCertificateRevocation = false,
        ServerCertificateValidationCallback = isTrusted,
    };

    private static SecureSocketOptions Security(SmtpServer server) =>
        server.ImplicitTls ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

    private static async Task QuitAsync(SmtpClient client, CancellationToken token) {
        try {
            await client.DisconnectAsync(true, token).ConfigureAwait(false);
        } catch (Exception error) when (SmtpFailures.IsSendError(error)) {
            return;
        }
    }

    private SendFailure? LoginFailure() {
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

    private async Task LogInAsync(SmtpClient client, CancellationToken token) {
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password)) {
            return;
        }
        try {
            await client.AuthenticateAsync(user, password, token).ConfigureAwait(false);
        } catch (ArgumentException error) {
            throw new AuthenticationException(unreadableChallengeMessage, error);
        }
    }

    private async Task TransmitAsync(SmtpClient client, MimeMessage message, CancellationToken token) {
        await LogInAsync(client, token).ConfigureAwait(false);
        await client.SendAsync(message, token).ConfigureAwait(false);
        await QuitAsync(client, token).ConfigureAwait(false);
    }

    private async Task<SendFailure?> SendMessageAsync(
        SmtpServer server,
        MimeMessage message,
        EmailAddresses addresses,
        CancellationToken cancellationToken) {
        using CancellationTokenSource deadline = new(SendLimits.Deadline, time);
        using CancellationTokenSource limited = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        using SmtpReplyLimit replyLimit = new();
        TimedStream? timed = null;
        bool certificateRefused = false;
        bool connected = false;
        try {
            if (await new SmtpSocket(transport, time).OpenAsync(server, limited.Token).ConfigureAwait(false) is not Socket socket) {
                return new SendFailure(SendFailureKind.SmtpUnreachable, messages.EmailSmtpUnreachable(server.Host, server.Port));
            }
            timed = new TimedStream(new NetworkStream(socket, ownsSocket: true), SendLimits.Operation, time);
            using SmtpClient client = NewClient(replyLimit, (_, certificate, _, errors) => {
                bool trusted = transport.IsTrusted(certificate, errors);
                certificateRefused |= !trusted;
                return trusted;
            });
            await client.ConnectAsync(timed, server.Host, server.Port, Security(server), limited.Token).ConfigureAwait(false);
            connected = true;
            await TransmitAsync(client, message, limited.Token).ConfigureAwait(false);
            return null;
        } catch (Exception error) when (cancellationToken.IsCancellationRequested) {
            throw new OperationCanceledException(canceledMessage, error, cancellationToken);
        } catch (Exception error) when (SmtpFailures.IsSendError(error)) {
            SmtpSendState state = new(
                server,
                addresses,
                deadline.IsCancellationRequested,
                replyLimit.IsOver,
                replyLimit.SawNonReply,
                timed?.TimedOut ?? false,
                certificateRefused,
                connected);
            return SmtpFailures.Of(error, state, messages);
        } finally {
            timed?.Dispose();
        }
    }
}
