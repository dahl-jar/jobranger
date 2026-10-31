using System.Net.Sockets;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Jobranger.Cli.Tests.Features.Delivery;

public sealed class SmtpFailuresTests {
    private const string detail = "server detail";
    private const string host = "mail.example.test";
    private const int port = 587;

    private static readonly IMessages messages = new EnglishMessages();

    private static readonly SmtpSendState quiet = new(
        new SmtpServer(host, port, ImplicitTls: false),
        DeliveryFixtures.Addresses,
        DeadlinePassed: false,
        ReplyTooLong: false,
        NonReply: false,
        TimedOut: false,
        CertificateRefused: false,
        Connected: false);

    private static readonly Dictionary<string, (Exception Error, SmtpSendState State)> rows = new() {
        ["not smtp"] = (new SmtpProtocolException(detail), quiet with { NonReply = true }),
        ["disconnected"] = (new SmtpProtocolException(detail), quiet),
        ["login status"] = (new AuthenticationException(detail, Command(SmtpErrorCode.UnexpectedStatusCode)), quiet),
        ["login"] = (new AuthenticationException(detail), quiet),
        ["recipient"] = (Command(SmtpErrorCode.RecipientNotAccepted), quiet),
        ["sender"] = (Command(SmtpErrorCode.SenderNotAccepted), quiet),
        ["status"] = (Command(SmtpErrorCode.UnexpectedStatusCode), quiet),
        ["not authenticated"] = (new ServiceNotAuthenticatedException(detail), quiet),
        ["certificate"] = (new SslHandshakeException(detail), quiet with { CertificateRefused = true }),
        ["tls"] = (new SslHandshakeException(detail), quiet),
        ["unsupported"] = (new NotSupportedException(detail), quiet with { Connected = true }),
        ["no starttls"] = (new NotSupportedException(detail), quiet),
        ["invalid host"] = (new ArgumentException(detail), quiet),
        ["io"] = (new IOException(detail), quiet),
        ["socket"] = (new SocketException(), quiet),
        ["other"] = (new FormatException(detail), quiet),
        ["invalid operation"] = (new InvalidOperationException(detail), quiet),
    };

    [Theory]
    [InlineData("not smtp", nameof(SendFailureKind.NotSmtp), null, "The server at mail.example.test:587 is not an SMTP server.")]
    [InlineData("disconnected", nameof(SendFailureKind.SmtpDisconnected), null, "The SMTP server at mail.example.test:587 closed the connection.")]
    [InlineData("login status", nameof(SendFailureKind.SmtpLogin), 550, "SMTP login to mail.example.test:587 failed.")]
    [InlineData("login", nameof(SendFailureKind.SmtpLogin), null, "SMTP login to mail.example.test:587 failed.")]
    [InlineData("recipient", nameof(SendFailureKind.SmtpRecipient), 550, "The server refused " + DeliveryFixtures.Address + ".")]
    [InlineData("sender", nameof(SendFailureKind.SmtpSender), 550, "The server refused the sender " + DeliveryFixtures.SenderMailbox + ".")]
    [InlineData("status", nameof(SendFailureKind.SmtpStatus), 550, "The SMTP server answered 550.")]
    [InlineData("not authenticated", nameof(SendFailureKind.SmtpSender), null, "The server refused the sender " + DeliveryFixtures.SenderMailbox + ".")]
    [InlineData("certificate", nameof(SendFailureKind.TlsCertificate), null, "Could not verify the TLS certificate of mail.example.test.")]
    [InlineData("tls", nameof(SendFailureKind.Tls), null, "TLS with mail.example.test:587 failed.")]
    [InlineData("unsupported", nameof(SendFailureKind.SmtpUnsupported), null, "The SMTP server at mail.example.test:587 does not support login or this address.")]
    [InlineData("no starttls", nameof(SendFailureKind.NoStarttls), null, "The SMTP server at mail.example.test:587 does not offer STARTTLS.")]
    [InlineData("invalid host", nameof(SendFailureKind.InvalidHost), null, "The SMTP server name mail.example.test is not a valid host name.")]
    [InlineData("io", nameof(SendFailureKind.SmtpUnreachable), null, "Could not reach mail.example.test:587.")]
    [InlineData("socket", nameof(SendFailureKind.SmtpUnreachable), null, "Could not reach mail.example.test:587.")]
    [InlineData("other", nameof(SendFailureKind.SmtpFailed), null, "The email could not be sent through mail.example.test:587.")]
    [InlineData("invalid operation", nameof(SendFailureKind.SmtpFailed), null, "The email could not be sent through mail.example.test:587.")]
    public void ShouldExplainFailure_WhenSendThrows(string row, string kind, int? status, string reason) {
        (Exception error, SmtpSendState state) = rows[row];

        SendFailure failure = SmtpFailures.Of(error, state, messages);

        Assert.Equal(new SendFailure(Enum.Parse<SendFailureKind>(kind), reason, error.GetType().Name, status), failure);
    }

    [Fact]
    public void ShouldPreferDeadline_WhenSeveralFlagsAreSet() {
        SmtpSendState state = new(
            quiet.Server, quiet.Addresses, true, true, true, true, true, true);

        SendFailure failure = SmtpFailures.Of(new IOException(detail), state, messages);

        Assert.Equal(SendFailureKind.SmtpTooSlow, failure.Kind);
        Assert.Contains("120 s", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldReportStall_WhenStreamTimedOut() {
        SendFailure failure = SmtpFailures.Of(new IOException(detail), quiet with { TimedOut = true }, messages);

        Assert.Equal(SendFailureKind.Stalled, failure.Kind);
    }

    [Fact]
    public void ShouldPreferLongReply_WhenStreamAlsoTimedOut() {
        SmtpSendState state = quiet with { ReplyTooLong = true, TimedOut = true };

        SendFailure failure = SmtpFailures.Of(Command(SmtpErrorCode.UnexpectedStatusCode), state, messages);

        Assert.Equal(SendFailureKind.SmtpReplyTooLong, failure.Kind);
    }

    [Fact]
    public void ShouldTreatKnownErrorAsSendFailure_WhenItIsClassified() {
        Assert.All(rows.Values, row => Assert.True(SmtpFailures.IsSendError(row.Error)));
    }

    [Fact]
    public void ShouldNotTreatErrorAsSendFailure_WhenItIsAKeyLookupBug() {
        Assert.False(SmtpFailures.IsSendError(new KeyNotFoundException()));
    }

    private static SmtpCommandException Command(SmtpErrorCode code) => new(
        code, SmtpStatusCode.MailboxUnavailable, MailboxAddress.Parse(DeliveryFixtures.Address), detail);
}
