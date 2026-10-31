using System.Net.Sockets;
using Jobranger.Cli.Text;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Jobranger.Cli.Features.Delivery;

internal static class SmtpFailures {
    public static bool IsSendError(Exception error) => error
        is ProtocolException
        or CommandException
        or AuthenticationException
        or SslHandshakeException
        or NotSupportedException
        or IOException
        or SocketException
        or TimeoutException
        or OperationCanceledException
        or ArgumentException
        or InvalidOperationException
        or FormatException
        or SmtpReplyTooLongException;

    public static SendFailure Of(Exception error, SmtpSendState state, IMessages messages) {
        string host = state.Server.Host;
        int port = state.Server.Port;
        string type = error.GetType().Name;
        (SendFailureKind kind, string reason, int? status) = (error, state) switch {
            (_, { DeadlinePassed: true }) =>
                (SendFailureKind.SmtpTooSlow, messages.EmailSmtpTooSlow(host, port, SendLimits.DeadlineSeconds), null),
            (_, { ReplyTooLong: true }) =>
                (SendFailureKind.SmtpReplyTooLong, messages.EmailSmtpReplyTooLong(host, port), null),
            (_, { TimedOut: true }) => (SendFailureKind.Stalled, messages.EmailStalled(host, port), null),
            (SmtpProtocolException, { NonReply: true }) => (SendFailureKind.NotSmtp, messages.EmailNotSmtp(host, port), null),
            (SmtpProtocolException, _) => (SendFailureKind.SmtpDisconnected, messages.EmailSmtpDisconnected(host, port), null),
            (AuthenticationException login, _) => (SendFailureKind.SmtpLogin, messages.EmailSmtpLoginFailed(host, port), LoginStatus(login)),
            (SmtpCommandException command, _) => CommandFailure(command, state, messages),
            (ServiceNotAuthenticatedException, _) =>
                (SendFailureKind.SmtpSender, messages.EmailSmtpSenderRefused(state.Addresses.Sender), null),
            (SslHandshakeException, { CertificateRefused: true }) =>
                (SendFailureKind.TlsCertificate, messages.EmailTlsCertificate(host), null),
            (SslHandshakeException, _) => (SendFailureKind.Tls, messages.EmailTlsFailed(host, port), null),
            (NotSupportedException, { Connected: true }) =>
                (SendFailureKind.SmtpUnsupported, messages.EmailSmtpUnsupported(host, port), null),
            (NotSupportedException, _) => (SendFailureKind.NoStarttls, messages.EmailNoStarttls(host, port), null),
            (ArgumentException, { Connected: false }) => (SendFailureKind.InvalidHost, messages.EmailInvalidHost(host), null),
            (IOException or SocketException, { Connected: true }) =>
                (SendFailureKind.SmtpDisconnected, messages.EmailSmtpDisconnected(host, port), null),
            (IOException or SocketException, _) => (SendFailureKind.SmtpUnreachable, messages.EmailSmtpUnreachable(host, port), null),
            _ => (SendFailureKind.SmtpFailed, messages.EmailSmtpFailed(host, port), (int?)null),
        };
        return new SendFailure(kind, reason, type, status);
    }

    private static int? LoginStatus(AuthenticationException error) =>
        error.InnerException is SmtpCommandException command ? (int)command.StatusCode : null;

    private static (SendFailureKind Kind, string Reason, int? Status) CommandFailure(
        SmtpCommandException error, SmtpSendState state, IMessages messages) {
        int status = (int)error.StatusCode;
        return error.ErrorCode switch {
            SmtpErrorCode.RecipientNotAccepted =>
                (SendFailureKind.SmtpRecipient, messages.EmailSmtpRecipientRefused(state.Addresses.To), status),
            SmtpErrorCode.SenderNotAccepted =>
                (SendFailureKind.SmtpSender, messages.EmailSmtpSenderRefused(state.Addresses.Sender), status),
            _ => (SendFailureKind.SmtpStatus, messages.EmailSmtpStatus(status), status),
        };
    }
}
