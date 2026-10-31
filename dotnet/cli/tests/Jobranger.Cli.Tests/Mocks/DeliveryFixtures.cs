using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using DeliveryEntry = Jobranger.Cli.Features.Delivery.Delivery;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Mocks;

internal static class DeliveryFixtures {
    public const string Address = "user@example.test";
    public const string Sender = "sender@example.test";
    public const string SenderMailbox = "jobranger <" + Sender + ">";
    public const string EmptyJson = "{}";
    public const string SmtpUrl = "smtp://mail.example.test";
    public const string ResendKey = "re_test_key";
    public const string Site = "https://site.example.test";

    private const string resendVariable = "RESEND_API_KEY";
    private const string secretsSuffix = ".secrets.json";
    private const string reportedQuery = "SELECT id FROM ads WHERE reported_at IS NOT NULL ORDER BY id";

    public static readonly TimeSpan SlowAnswer = TimeSpan.FromSeconds(29);

    public static readonly EmailAddresses Addresses = new(SenderMailbox, Address);

    public static readonly RenderedDigest Rendered = new("Subject Ø", "<p>html</p>", "text", ["finn:1"]);

    public static readonly StoredClassification Classification = AdFixtures.Classification with {
        Assessment = MatchingEntry.EncodeMatch(MatchingFixtures.StrongMatch, []),
    };

    public static readonly EmailConfig ResendEmail = new() { On = true, Address = Address, Via = EmailVia.Resend };

    public static void AssertOneOperationPassed(FakeTimeProvider time, DateTimeOffset started) {
        TimeSpan passed = time.GetUtcNow() - started;
        Assert.InRange(passed, SendLimits.Operation, (SendLimits.Operation * 2) - TimeSpan.FromTicks(1));
    }

    public static AppConfig Config(EmailConfig email, bool website = false) => new() {
        Profile = new ProfileRef("profile.yaml"),
        Targets = MatchingFixtures.AllRoles(),
        Notify = new NotifyConfig { Website = website, Email = email },
    };

    public static RunResult Result(params Ad[] ads) => new() {
        Status = RunStatus.Completed,
        ReportedIds = [.. ads.Select(ad => ad.Id)],
        Version = MatchingFixtures.Version,
    };

    public static Ad Stored(AdsEntry ads, Ad ad, StoredClassification? classification = null) {
        ads.Upsert(ad);
        ads.SetClassification(ad.Id, classification ?? Classification, MatchingFixtures.Version);
        return ad;
    }

    [UnsupportedOSPlatform("windows")]
    public static string SecretsPath(TestDatabase db) => db.Path + secretsSuffix;

    [UnsupportedOSPlatform("windows")]
    public static Credentials WithResendKey(TestDatabase db) => Credentials(db, new Hashtable { [resendVariable] = ResendKey });

    [UnsupportedOSPlatform("windows")]
    public static Credentials Credentials(TestDatabase db, IDictionary environment, ISecretStore? store = null) =>
        new(environment, store, new FileSecretStore(SecretsPath(db), NullLogger.Instance));

    [UnsupportedOSPlatform("windows")]
    public static DeliveryEntry Create(
        TestDatabase db,
        FakeApiHandler handler,
        FakeTimeProvider time,
        IMessages? messages = null,
        ILogger? logger = null) =>
        Create(db, TransportFor(() => handler), time, WithResendKey(db), messages, logger);

    [UnsupportedOSPlatform("windows")]
    public static DeliveryEntry Create(
        TestDatabase db,
        DeliveryTransport transport,
        FakeTimeProvider time,
        Credentials credentials,
        IMessages? messages = null,
        ILogger? logger = null) => new(
            db.Connection,
            credentials,
            new ServiceUrls(new Uri(Site), new Uri("https://relay.example.test")),
            messages ?? new EnglishMessages(),
            transport,
            time,
            logger ?? NullLogger.Instance);

    public static DeliveryTransport TransportFor(Func<HttpMessageHandler> create) =>
        DeliveryTransport.Production with { CreateResendHandler = create };

    [UnsupportedOSPlatform("windows")]
    public static List<string> ReportedIds(TestDatabase db) => TestDatabase.Rows(db.Connection, reportedQuery);
}
