using System.Net;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Time.Testing;
using MimeKit;

namespace Jobranger.Cli.Tests.Features.Delivery;

public sealed class SmtpSenderTests {
    private const string user = "user";
    private const string password = "pass-1234";
    private const string startTls = "STARTTLS";
    private const string authPlain = "AUTH PLAIN";
    private const string longReply = "250-x\r\n";
    private const string lastLine = "250 ok\r\n";
    private const int tooManyLines = 100;

    private readonly FakeTimeProvider time = AdFixtures.Clock();

    [Fact]
    public async Task ShouldWriteMessage_WhenServerAccepts() {
        await using FakeSmtpServer server = FakeSmtpServer.Accepting();

        await SendAsync(server.Url("smtp"));

        MimeMessage message = MimeMessage.Load(new MemoryStream(server.Messages[0]), TestContext.Current.CancellationToken);
        Multipart body = Assert.IsType<Multipart>(message.Body, exactMatch: false);
        Assert.Equal(DeliveryFixtures.Rendered.Subject, message.Subject);
        Assert.Equal(DeliveryFixtures.Sender, message.From.Mailboxes.Single().Address);
        Assert.Equal(DeliveryFixtures.Address, message.To.Mailboxes.Single().Address);
        Assert.Equal("text/plain", body[0].ContentType.MimeType);
        Assert.Equal("text/html", body[1].ContentType.MimeType);
        Assert.Equal(AdFixtures.Now, message.Date);
        Assert.EndsWith("@example.test", message.MessageId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldLogIn_WhenUserIsSet() {
        await using FakeSmtpServer server = FakeSmtpServer.Accepting(startTls, authPlain);

        await SendAsync(server.Url("smtp"), user, password);

        string decoded = server.DecodedLogin();
        Assert.Equal($"\0{user}\0{password}", decoded);
    }

    [Fact]
    public async Task ShouldSkipLogin_WhenUserIsMissing() {
        await using FakeSmtpServer server = FakeSmtpServer.Accepting(startTls, authPlain);

        await SendAsync(server.Url("smtp"), null, password);

        Assert.Single(server.Messages);
        Assert.DoesNotContain(server.Commands, command => command.StartsWith("AUTH", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldGreetWithoutMachineName_WhenConnecting() {
        await using FakeSmtpServer server = FakeSmtpServer.Accepting();

        await SendAsync(server.Url("smtp"));

        Assert.Equal("EHLO [127.0.0.1]", server.Commands[0]);
    }

    [Fact]
    public async Task ShouldRefuseSend_WhenServerLacksStarttls() {
        await using FakeSmtpServer server = FakeSmtpServer.Accepting("8BITMIME");

        SendFailure? failure = await SendAsync(server.Url("smtp"), user, password);

        Assert.Equal(SendFailureKind.NoStarttls, failure?.Kind);
        Assert.DoesNotContain(server.Commands, command => command.StartsWith("MAIL", StringComparison.Ordinal));
        Assert.DoesNotContain(server.Commands, command => command.StartsWith("AUTH", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldStartWithTls_WhenSchemeIsSmtps() {
        await using FakeSmtpServer server = FakeSmtpServer.AcceptingTls();

        SendFailure? failure = await SendAsync(server.Url("smtps"));

        Assert.Null(failure);
        Assert.Single(server.Messages);
    }

    [Fact]
    public async Task ShouldRefuseCertificate_WhenServerIsNotTrusted() {
        await using FakeSmtpServer server = FakeSmtpServer.Accepting(startTls, authPlain);

        SendFailure? failure = await SendAsync(server.Url("smtp"), user, password, DeliveryTransport.Production);

        Assert.Equal(SendFailureKind.TlsCertificate, failure?.Kind);
        Assert.Contains("127.0.0.1", failure?.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(server.Commands, command => command.StartsWith("AUTH", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldReportTlsFailure_WhenHandshakeBreaks() {
        await using FakeSmtpServer server = new(async session => {
            await FakeSmtpServer.ReachStartTlsAsync(session);
            await session.ReadClientHelloAsync();
            await session.SendAsync("this is not a tls record\r\n");
            await session.StallAsync();
        });

        SendFailure? failure = await SendAsync(server.Url("smtp"));

        Assert.Equal(SendFailureKind.Tls, failure?.Kind);
    }

    [Fact]
    public async Task ShouldRefuseLongReply_WhenItArrivesAfterTls() {
        await using FakeSmtpServer server = new(async session => {
            await FakeSmtpServer.ReachStartTlsAsync(session);
            await session.UpgradeAsync();
            await session.ReadLineAsync();
            await session.SendAsync(ReplyOfTooManyLines());
            await session.StallAsync();
        });

        SendFailure? failure = await SendAsync(server.Url("smtp"));

        Assert.Equal(SendFailureKind.SmtpReplyTooLong, failure?.Kind);
    }

    [Fact]
    public async Task ShouldRefuseLongReply_WhenCommandsArePipelined() {
        await using FakeSmtpServer server = new(async session => {
            await FakeSmtpServer.ReachStartTlsAsync(session);
            await session.UpgradeAsync();
            await FakeSmtpServer.HelloAsync(session, "PIPELINING");
            await session.ReadLineAsync();
            await session.ReadLineAsync();
            await session.SendAsync("550 refused\r\n");
            await session.SendAsync(ReplyOfTooManyLines());
            await session.StallAsync();
        });
        Task<SendFailure?> sending = SendAsync(server.Url("smtp"));
        await server.Waiting;

        SendFailure? failure = await ApiClock.RunAsync(time, sending);

        Assert.Equal(SendFailureKind.SmtpReplyTooLong, failure?.Kind);
    }

    [Fact]
    public async Task ShouldStopSend_WhenSlowServerPasses120Seconds() {
        await using FakeSmtpServer server = FakeSmtpServer.SlowlyAccepting(() => time.Advance(DeliveryFixtures.SlowAnswer));

        SendFailure? failure = await SendAsync(server.Url("smtp"));

        Assert.Equal(SendFailureKind.SmtpTooSlow, failure?.Kind);
        Assert.DoesNotContain("DATA", server.Commands);
    }

    [Fact]
    public async Task ShouldReportStalled_WhenServerStopsAnswering() {
        await using FakeSmtpServer server = new(async session => {
            await session.SendAsync("220 fake ready\r\n");
            await session.ReadLineAsync();
            await session.StallAsync();
        });
        DateTimeOffset started = time.GetUtcNow();
        Task<SendFailure?> sending = SendAsync(server.Url("smtp"));
        await server.Waiting;

        SendFailure? failure = await ApiClock.RunAsync(time, sending);

        Assert.Equal(SendFailureKind.Stalled, failure?.Kind);
        DeliveryFixtures.AssertOneOperationPassed(time, started);
    }

    [Fact]
    public async Task ShouldReportUnreachable_WhenLookupHangs() {
        DeliveryTransport transport = SmtpCertificates.Trusting with {
            Resolve = (_, _) => new TaskCompletionSource<IPAddress[]>().Task,
        };
        DateTimeOffset started = time.GetUtcNow();

        SendFailure? failure = await ApiClock.RunAsync(time, SendAsync(DeliveryFixtures.SmtpUrl + ":587", transport: transport));

        Assert.Equal(SendFailureKind.SmtpUnreachable, failure?.Kind);
        DeliveryFixtures.AssertOneOperationPassed(time, started);
    }

    [Fact]
    public async Task ShouldTryNextAddress_WhenFirstRefuses() {
        await using FakeSmtpServer server = FakeSmtpServer.Accepting();
        DeliveryTransport transport = SmtpCertificates.Trusting with {
            Resolve = (_, _) => Task.FromResult<IPAddress[]>([IPAddress.IPv6Loopback, IPAddress.Loopback]),
        };

        SendFailure? failure = await SendAsync($"{DeliveryFixtures.SmtpUrl}:{server.Port}", transport: transport);

        Assert.Null(failure);
        Assert.Single(server.Messages);
    }

    [Fact]
    public async Task ShouldReportUnreachable_WhenNothingListens() {
        string url;
        await using (FakeSmtpServer closed = FakeSmtpServer.Accepting()) {
            url = closed.Url("smtp");
        }

        SendFailure? failure = await SendAsync(url);

        Assert.Equal(SendFailureKind.SmtpUnreachable, failure?.Kind);
    }

    [Fact]
    public async Task ShouldCountAsSent_WhenQuitStalls() {
        await using FakeSmtpServer server = FakeSmtpServer.AnsweringQuit(session => session.StallAsync());
        Task<SendFailure?> sending = SendAsync(server.Url("smtp"));
        await server.Waiting;

        SendFailure? failure = await ApiClock.RunAsync(time, sending);

        Assert.Null(failure);
        Assert.Single(server.Messages);
    }

    [Fact]
    public async Task ShouldCountAsSent_WhenQuitReplyIsTooLong() {
        await using FakeSmtpServer server = FakeSmtpServer.AnsweringQuit(session => session.SendAsync(ReplyOfTooManyLines()));

        SendFailure? failure = await SendAsync(server.Url("smtp"));

        Assert.Null(failure);
        Assert.Single(server.Messages);
    }

    [Fact]
    public async Task ShouldThrowCanceled_WhenCallerCancelsDuringTls() {
        using CancellationTokenSource caller = new();
        await using FakeSmtpServer server = new(async session => {
            await FakeSmtpServer.ReachStartTlsAsync(session);
            await caller.CancelAsync();
            await session.StallAsync();
        });

        await Assert.ThrowsAsync<OperationCanceledException>(() => SendWithAsync(server.Url("smtp"), SmtpCertificates.Trusting, caller.Token));
    }

    [Fact]
    public async Task ShouldThrowCanceled_WhenCallerCancelsDuringLookup() {
        using CancellationTokenSource caller = new();
        DeliveryTransport transport = SmtpCertificates.Trusting with {
            Resolve = (_, _) => {
                caller.Cancel();
                return new TaskCompletionSource<IPAddress[]>().Task;
            },
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => SendWithAsync(DeliveryFixtures.SmtpUrl + ":587", transport, caller.Token));
    }

    [Fact]
    public async Task ShouldThrowCanceled_WhenCallerCancelsBeforeConnect() {
        using CancellationTokenSource caller = new();
        await caller.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => SendWithAsync("smtp://127.0.0.1:587", SmtpCertificates.Trusting, caller.Token));
    }

    [Theory]
    [InlineData("smtp://", null, null, nameof(SendFailureKind.InvalidSmtpUrl))]
    [InlineData(DeliveryFixtures.SmtpUrl, user, null, nameof(SendFailureKind.MissingSmtpPassword))]
    [InlineData(DeliveryFixtures.SmtpUrl, user, "pæss", nameof(SendFailureKind.NonAsciiLogin))]
    [InlineData(DeliveryFixtures.SmtpUrl, "usér", password, nameof(SendFailureKind.NonAsciiLogin))]
    public async Task ShouldRefuseBeforeConnecting_WhenSettingIsUnusable(
        string url, string? login, string? secret, string kind) {
        DeliveryTransport transport = SmtpCertificates.Trusting with {
            Resolve = (_, _) => throw new InvalidOperationException("no lookup should run"),
        };

        SendFailure? failure = await SendAsync(url, login, secret, transport);

        Assert.Equal(Enum.Parse<SendFailureKind>(kind), failure?.Kind);
    }

    [Fact]
    public async Task ShouldRefuseAddress_WhenSenderDoesNotParse() {
        EmailAddresses addresses = DeliveryFixtures.Addresses with { Sender = "not a mailbox" };
        SmtpSender sender = new(
            DeliveryFixtures.SmtpUrl, null, null, SmtpCertificates.Trusting, new EnglishMessages(), time);

        SendFailure? failure = await sender.SendAsync(
            DeliveryFixtures.Rendered, addresses, TestContext.Current.CancellationToken);

        Assert.Equal(SendFailureKind.UnusableAddress, failure?.Kind);
    }

    private static string ReplyOfTooManyLines() =>
        string.Concat(Enumerable.Repeat(longReply, tooManyLines)) + lastLine;

    private Task<SendFailure?> SendAsync(
        string url,
        string? login = null,
        string? secret = null,
        DeliveryTransport? transport = null) =>
        new SmtpSender(url, login, secret, transport ?? SmtpCertificates.Trusting, new EnglishMessages(), time).SendAsync(
            DeliveryFixtures.Rendered, DeliveryFixtures.Addresses, TestContext.Current.CancellationToken)
            .WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);

    private Task<SendFailure?> SendWithAsync(string url, DeliveryTransport transport, CancellationToken cancellationToken) =>
        new SmtpSender(url, null, null, transport, new EnglishMessages(), time).SendAsync(
            DeliveryFixtures.Rendered, DeliveryFixtures.Addresses, cancellationToken)
            .WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);
}
