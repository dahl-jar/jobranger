using System.Collections;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Files;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using DeliveryEntry = Jobranger.Cli.Features.Delivery.Delivery;

namespace Jobranger.Cli.Tests.Features.Delivery;

[UnsupportedOSPlatform("windows")]
public sealed class DeliveryTests : IDisposable {
    private const int cutAdCount = 400;
    private const int summaryLength = 280;

    private readonly TestDatabase db = new();
    private readonly FakeTimeProvider time = AdFixtures.Clock();
    private readonly AdsEntry ads;

    public DeliveryTests() {
        ads = new AdsEntry(db.Connection, time);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task ShouldMarkAdsReported_WhenEmailIsSent() {
        Ad ad = StoredAd(1);

        DeliveryResult result = await DeliverAsync(FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson), DeliveryFixtures.Result(ad));

        Assert.Equal(DeliveryFixtures.Address, result.EmailTo);
        Assert.Equal([ad.Id], DeliveryFixtures.ReportedIds(db));
    }

    [Fact]
    public async Task ShouldKeepAdsUnreported_WhenSendFails() {
        Ad ad = StoredAd(1);

        DeliveryResult result = await DeliverAsync(FakeApiHandler.Json(500, DeliveryFixtures.EmptyJson), DeliveryFixtures.Result(ad));

        Assert.Equal("Resend answered 500.", result.EmailError);
        Assert.Empty(DeliveryFixtures.ReportedIds(db));
    }

    [Fact]
    public async Task ShouldMarkAdsWithoutSending_WhenEmailIsOff() {
        Ad ad = StoredAd(1);
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);
        AppConfig config = DeliveryFixtures.Config(DeliveryFixtures.ResendEmail with { On = false });

        DeliveryResult result = await DeliveryFixtures.Create(db, handler, time)
            .DeliverAsync(config, DeliveryFixtures.Result(ad), TestContext.Current.CancellationToken);

        Assert.Equal(new DeliveryResult(), result);
        Assert.Equal([ad.Id], DeliveryFixtures.ReportedIds(db));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ShouldSendNothing_WhenRunHasNoNews() {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);

        DeliveryResult result = await DeliverAsync(handler, DeliveryFixtures.Result());

        Assert.Equal(new DeliveryResult(), result);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("failed source")]
    [InlineData("budget")]
    [InlineData("provider down")]
    public async Task ShouldSendNotice_WhenRunHasNoMatches(string notice) {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);
        RunResult result = notice switch {
            "failed source" => DeliveryFixtures.Result() with { FailedSources = ["finn"] },
            "budget" => DeliveryFixtures.Result() with { ModelStopped = StopReason.Budget, Pending = 4 },
            _ => DeliveryFixtures.Result() with { ModelStopped = StopReason.ProviderDown, Pending = 4 },
        };

        await DeliverAsync(handler, result);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ShouldSendDigest_WhenOnlyAdIsDueSoon() {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);
        Ad due = DeliveryFixtures.Stored(ads, RunFixtures.Pending(1, "Developer", "Org AS") with { Deadline = "2026-10-12" });

        await DeliverAsync(handler, DeliveryFixtures.Result(due));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ShouldSendDigest_WhenOnlyAdIsCarriedOver() {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);
        Ad carried = StoredAd(1);

        await DeliverAsync(handler, DeliveryFixtures.Result(carried) with { CarriedIds = [carried.Id] });

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ShouldReportClosedAds_WhenPreviousRunExists() {
        Ad closed = StoredAd(1);
        RunStore runs = new(db.Connection);
        runs.Start(time.GetUtcNow());
        time.Advance(TimeSpan.FromHours(1));
        ads.SetActive(closed.Id, false);
        time.Advance(TimeSpan.FromHours(1));
        long current = runs.Start(time.GetUtcNow());
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);

        await DeliverAsync(handler, DeliveryFixtures.Result() with { RunId = current, FailedSources = ["finn"] });

        Assert.Contains("One earlier job closed since the last run.", Payload(handler, "text"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldLinkToSiteOnly_WhenWebsiteIsOn(bool website) {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);
        AppConfig config = DeliveryFixtures.Config(DeliveryFixtures.ResendEmail, website);

        await DeliveryFixtures.Create(db, handler, time)
            .DeliverAsync(config, DeliveryFixtures.Result(StoredAd(1)), TestContext.Current.CancellationToken);

        string html = Payload(handler, "html");
        Assert.Equal(website, html.Contains(DeliveryFixtures.Site + "/app", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldDateDigestByLocalDay_WhenUtcDayDiffers() {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);
        FakeTimeProvider plusTwo = PlusTwoAt(new DateTimeOffset(2026, 10, 8, 23, 30, 0, TimeSpan.Zero));

        await DeliverStoredAdAsync(DeliveryFixtures.Create(db, handler, plusTwo));

        Assert.Contains("Friday 9 October", Payload(handler, "subject"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldUseLocalHourInLead_WhenUtcHourDiffers() {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);
        FakeTimeProvider plusTwo = PlusTwoAt(new DateTimeOffset(2026, 10, 8, 16, 30, 0, TimeSpan.Zero));

        await DeliverStoredAdAsync(DeliveryFixtures.Create(db, handler, plusTwo));

        Assert.Contains("this evening", Payload(handler, "text"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(RunStatus.Locked))]
    [InlineData(nameof(RunStatus.Cancelled))]
    public async Task ShouldNotDeliver_WhenPassDidNotFinish(string status) {
        Ad silent = StoredAd(1);
        Ad reported = StoredAd(2);
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);
        RunResult run = DeliveryFixtures.Result(reported) with {
            Status = Enum.Parse<RunStatus>(status),
            SilentIds = [silent.Id],
        };

        DeliveryResult result = await DeliverAsync(handler, run);

        Assert.Equal(new DeliveryResult(), result);
        Assert.Empty(handler.Requests);
        Assert.Empty(DeliveryFixtures.ReportedIds(db));
    }

    [Fact]
    public async Task ShouldDeliver_WhenPassFailed() {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);
        RunResult run = DeliveryFixtures.Result() with { Status = RunStatus.Failed, FailedSources = ["finn"] };

        await DeliverAsync(handler, run);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ShouldMarkOnlyShownAds_WhenDigestIsCutForSize() {
        Ad[] stored = [.. Enumerable.Range(1, cutAdCount).Select(index => DeliveryFixtures.Stored(
            ads,
            RunFixtures.Pending(index, $"Developer {index}", $"Org {index} AS"),
            DeliveryFixtures.Classification with { Summary = new string('x', summaryLength) }))];
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);

        await DeliverAsync(handler, DeliveryFixtures.Result(stored));

        string html = Payload(handler, "html");
        string[] shown = [.. stored
            .Where(ad => html.Contains($"\"{ad.Url}\"", StringComparison.Ordinal))
            .Select(ad => ad.Id)
            .Order(StringComparer.Ordinal)];
        Assert.InRange(shown.Length, 1, cutAdCount - 1);
        Assert.Equal(shown, DeliveryFixtures.ReportedIds(db));
    }

    [Fact]
    public async Task ShouldMarkSilentAds_WhenSendFails() {
        Ad silent = StoredAd(1);
        Ad reported = StoredAd(2);
        RunResult run = DeliveryFixtures.Result(reported) with { SilentIds = [silent.Id] };

        await DeliverAsync(FakeApiHandler.Json(500, DeliveryFixtures.EmptyJson), run);

        Assert.Equal([silent.Id], DeliveryFixtures.ReportedIds(db));
    }

    [Fact]
    public async Task ShouldReuseKey_WhenSameRunIsDeliveredAgain() {
        List<FakeApiHandler> handlers = [];
        DeliveryTransport transport = DeliveryFixtures.TransportFor(() => {
            FakeApiHandler created = FakeApiHandler.Json(500, DeliveryFixtures.EmptyJson);
            handlers.Add(created);
            return created;
        });
        DeliveryEntry delivery = DeliveryFixtures.Create(db, transport, time, DeliveryFixtures.WithResendKey(db));
        RunResult run = DeliveryFixtures.Result(StoredAd(1)) with { RunId = 7 };
        AppConfig config = DeliveryFixtures.Config(DeliveryFixtures.ResendEmail);

        await delivery.DeliverAsync(config, run, TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(1));
        await delivery.DeliverAsync(config, run, TestContext.Current.CancellationToken);
        await delivery.DeliverAsync(config, run with { RunId = 8 }, TestContext.Current.CancellationToken);

        string? first = handlers[0].Last.Header("Idempotency-Key");
        Assert.NotNull(first);
        Assert.Equal(first, handlers[1].Last.Header("Idempotency-Key"));
        Assert.NotEqual(first, handlers[2].Last.Header("Idempotency-Key"));
    }

    [Theory]
    [InlineData("no address", "No email address is set. Add one with: jobranger init")]
    [InlineData("unusable address", "The email address or sender in the config cannot be used.")]
    [InlineData("no smtp url", "No SMTP server is set. Add one with: jobranger init")]
    [InlineData("no resend key", "No Resend API key is stored. Add one with: jobranger init")]
    [InlineData("no smtp password", "No SMTP password is stored. Add one with: jobranger init")]
    public async Task ShouldRefuseSend_WhenSettingIsMissing(string setting, string reason) {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);
        EmailConfig smtp = new() { On = true, Address = DeliveryFixtures.Address };
        EmailConfig email = setting switch {
            "no address" => DeliveryFixtures.ResendEmail with { Address = null },
            "unusable address" => DeliveryFixtures.ResendEmail with { Address = "not an address" },
            "no smtp url" => smtp,
            "no smtp password" => smtp with { SmtpUrl = DeliveryFixtures.SmtpUrl, SmtpUser = "user" },
            _ => DeliveryFixtures.ResendEmail,
        };
        DeliveryEntry delivery = DeliveryFixtures.Create(
            db, DeliveryFixtures.TransportFor(() => handler), time, DeliveryFixtures.Credentials(db, new Hashtable()));

        DeliveryResult result = await delivery.DeliverAsync(
            DeliveryFixtures.Config(email), DeliveryFixtures.Result(StoredAd(1)), TestContext.Current.CancellationToken);

        Assert.Equal(reason, result.EmailError);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ShouldReturnFailure_WhenSecretStoreBreaks() {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);
        DeliveryEntry delivery = DeliveryFixtures.Create(
            db,
            DeliveryFixtures.TransportFor(() => handler),
            time,
            DeliveryFixtures.Credentials(db, new Hashtable(), new FakeSecretStore { IsBroken = true }));

        DeliveryResult result = await DeliverStoredAdAsync(delivery);

        Assert.Equal(new EnglishMessages().EmailSecretStore, result.EmailError);
    }

    [Fact]
    public async Task ShouldReturnFailure_WhenSecretsFileIsCorrupt() {
        File.WriteAllText(DeliveryFixtures.SecretsPath(db), "not json");
        FakeLogger logger = new();
        DeliveryEntry delivery = DeliveryFixtures.Create(
            db, DeliveryTransport.Production, time, DeliveryFixtures.Credentials(db, new Hashtable()), logger: logger);

        DeliveryResult result = await DeliverStoredAdAsync(delivery);

        FakeLogRecord record = logger.Collector.GetSnapshot().Single(entry => entry.Id.Name == "email.failed");
        Assert.Equal(new EnglishMessages().EmailSecretStore, result.EmailError);
        Assert.Equal(nameof(PrivateFileException), record.GetStructuredStateValue("ErrorType"));
    }

    [Fact]
    public async Task ShouldLogCardCount_WhenEmailIsSent() {
        FakeLogger logger = new();

        await DeliverStoredAdAsync(DeliveryFixtures.Create(db, FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson), time, logger: logger));

        FakeLogRecord record = logger.Collector.GetSnapshot().Single(entry => entry.Id.Name == "email.sent");
        Assert.Equal("1", record.GetStructuredStateValue("Count"));
    }

    [Fact]
    public async Task ShouldLogFailureWithoutAddress_WhenSendFails() {
        FakeLogger logger = new();

        await DeliverStoredAdAsync(DeliveryFixtures.Create(db, FakeApiHandler.Json(500, DeliveryFixtures.EmptyJson), time, logger: logger));

        FakeLogRecord record = logger.Collector.GetSnapshot().Single(entry => entry.Id.Name == "email.failed");
        Assert.Equal("resend_status", record.GetStructuredStateValue("Failure"));
        Assert.Equal("500", record.GetStructuredStateValue("Status"));
        Assert.DoesNotContain(record.StructuredState!, field => field.Value?.Contains(DeliveryFixtures.Address, StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ShouldSendThroughSmtp_WhenConfigChoosesSmtp() {
        await using FakeSmtpServer server = FakeSmtpServer.Accepting();
        Ad ad = StoredAd(1);
        DeliveryEntry delivery = DeliveryFixtures.Create(
            db, SmtpCertificates.Trusting, time, DeliveryFixtures.Credentials(db, new Hashtable()));
        EmailConfig email = new() { On = true, Address = DeliveryFixtures.Address, SmtpUrl = server.Url("smtp") };

        DeliveryResult result = await delivery.DeliverAsync(
            DeliveryFixtures.Config(email), DeliveryFixtures.Result(ad), TestContext.Current.CancellationToken)
            .WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);

        Assert.Equal(DeliveryFixtures.Address, result.EmailTo);
        Assert.Single(server.Messages);
        Assert.Equal([ad.Id], DeliveryFixtures.ReportedIds(db));
    }

    [Fact]
    public async Task ShouldLogInWithStoredPassword_WhenConfigChoosesSmtp() {
        await using FakeSmtpServer server = FakeSmtpServer.Accepting("STARTTLS", "AUTH PLAIN");
        DeliveryEntry delivery = DeliveryFixtures.Create(
            db,
            SmtpCertificates.Trusting,
            time,
            DeliveryFixtures.Credentials(db, new Hashtable { ["SMTP_PASSWORD"] = "pass-1234" }));
        EmailConfig email = new() {
            On = true,
            Address = DeliveryFixtures.Address,
            SmtpUrl = server.Url("smtp"),
            SmtpUser = "user",
        };

        await delivery.DeliverAsync(
            DeliveryFixtures.Config(email), DeliveryFixtures.Result(StoredAd(1)), TestContext.Current.CancellationToken)
            .WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);

        string decoded = server.DecodedLogin();
        Assert.Equal("\0user\0pass-1234", decoded);
    }

    [Fact]
    public async Task ShouldExplainFailureInNorwegian_WhenMessagesAreNorwegian() {
        DeliveryEntry delivery = DeliveryFixtures.Create(db, FakeApiHandler.Json(500, DeliveryFixtures.EmptyJson), time, new NorwegianMessages());

        DeliveryResult result = await DeliverStoredAdAsync(delivery);

        Assert.Equal("Resend svarte 500.", result.EmailError);
    }

    [Fact]
    public async Task ShouldWriteDigestInEnglish_WhenMessagesAreNorwegian() {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);

        await DeliverStoredAdAsync(DeliveryFixtures.Create(db, handler, time, new NorwegianMessages()));

        Assert.Contains("Friday 9 October", Payload(handler, "subject"), StringComparison.Ordinal);
        Assert.Contains("this morning", Payload(handler, "text"), StringComparison.Ordinal);
    }

    private static string Payload(FakeApiHandler handler, string field) =>
        (string)JsonNode.Parse(handler.Last.Text)![field]!;

    private static FakeTimeProvider PlusTwoAt(DateTimeOffset now) {
        FakeTimeProvider clock = new(now);
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("plus-two", TimeSpan.FromHours(2), "plus-two", "plus-two"));
        return clock;
    }

    private Ad StoredAd(int index) =>
        DeliveryFixtures.Stored(ads, RunFixtures.Pending(index, $"Developer {index}", $"Org {index} AS"));

    private Task<DeliveryResult> DeliverStoredAdAsync(DeliveryEntry delivery) =>
        delivery.DeliverAsync(
            DeliveryFixtures.Config(DeliveryFixtures.ResendEmail),
            DeliveryFixtures.Result(StoredAd(1)),
            TestContext.Current.CancellationToken);

    private Task<DeliveryResult> DeliverAsync(FakeApiHandler handler, RunResult result) =>
        DeliveryFixtures.Create(db, handler, time)
            .DeliverAsync(DeliveryFixtures.Config(DeliveryFixtures.ResendEmail), result, TestContext.Current.CancellationToken);
}
