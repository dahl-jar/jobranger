using System.IO.Compression;
using System.IO.Pipelines;
using System.Net;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Http;
using Jobranger.Cli.Tests.Http;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;
using static Jobranger.Cli.Tests.Mocks.AdFixtures;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;

namespace Jobranger.Cli.Tests.Features.Ads;

[UnsupportedOSPlatform("windows")]
public sealed class ApplyLinksTests {
    private const string applyUrl = "https://apply.example.test/jobs/1";
    private const string otherApplyUrl = "https://apply.example.test/jobs/2";
    private const string dataUrl = "data:text/html,hi";
    private const string activePage = "<html>Søk nå</html>";
    private const string expiredMarker = "status:\"EXPIRED\"";
    private const string stoppedMarker = "status:\"STOPPED\"";
    private const long hostileBodyBytes = 20L * 1024 * 1024;
    private const int applyCheckLimit = 60;
    private const string statusQuery = "SELECT id, apply_status, apply_checked_at FROM ads ORDER BY id";
    private const string gzipEncoding = "gzip";
    private const string brotliEncoding = "br";
    private const byte corruptByte = 0xFF;
    private const int corruptBodyBytes = 64;
    private const int testTimeoutMs = 10_000;
    private const string retryAfterHeader = "Retry-After";
    private const string longRetryAfter = "100000";

    private static readonly TimeSpan checkDeadline = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan dripGap = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan settle = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ShouldReturnBlocked_WhenApplyLinkRedirectsToDataUrl() {
        FakeSourceHandler handler = new(_ => HttpFixtures.RawRedirect(dataUrl));

        ApplyStatus status = await CheckAsync(handler, applyUrl);

        Assert.Equal(ApplyStatus.Blocked, status);
    }

    [Fact]
    public async Task ShouldReturnBlocked_WhenRedirectHasNoLocation() {
        FakeSourceHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.Found));

        ApplyStatus status = await CheckAsync(handler, applyUrl);

        Assert.Equal(ApplyStatus.Blocked, status);
    }

    [Fact]
    public async Task ShouldReturnBlocked_WhenHostResolvesToPrivateAddress() {
        FakeSourceHandler handler = new(_ => throw new HttpRequestException(
            HttpRequestError.ConnectionError, null, new RefusedRequestException("private address")));

        ApplyStatus status = await CheckAsync(handler, applyUrl);

        Assert.Equal(ApplyStatus.Blocked, status);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task ShouldReturnExpired_WhenPageIsGone(HttpStatusCode code) {
        FakeSourceHandler handler = new(_ => new HttpResponseMessage(code));

        ApplyStatus status = await CheckAsync(handler, applyUrl);

        Assert.Equal(ApplyStatus.Expired, status);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ShouldReturnUnknown_WhenPageAnswersError(HttpStatusCode code) {
        FakeSourceHandler handler = new(_ => new HttpResponseMessage(code));

        ApplyStatus status = await CheckAsync(handler, applyUrl);

        Assert.Equal(ApplyStatus.Unknown, status);
    }

    [Theory]
    [InlineData("https://www.finn.no/job/1")]
    [InlineData("https://finn.no/job/1")]
    [InlineData("https://login.vend.no/login")]
    public async Task ShouldReturnSkipped_WhenRedirectEndsOnFinn(string location) {
        FakeSourceHandler handler = new(index => index == 0 ? HttpFixtures.RawRedirect(location) : Page(activePage));

        ApplyStatus status = await CheckAsync(handler, applyUrl);

        Assert.Equal(ApplyStatus.Skipped, status);
    }

    [Theory]
    [InlineData("https://thehub.io/jobs/1", "<script>{" + expiredMarker + "}</script>")]
    [InlineData("https://www.thehub.io/jobs/1", "<script>{" + stoppedMarker + "}</script>")]
    [InlineData("https://thehub.io/jobs/1", expiredMarker)]
    public async Task ShouldReturnExpired_WhenTheHubPageShowsClosedMarker(string url, string html) {
        FakeSourceHandler handler = new(_ => Page(html));

        ApplyStatus status = await CheckAsync(handler, url);

        Assert.Equal(ApplyStatus.Expired, status);
    }

    [Fact]
    public async Task ShouldReturnActive_WhenMarkerIsOnOtherHost() {
        FakeSourceHandler handler = new(_ => Page("<script>{" + expiredMarker + "}</script>"));

        ApplyStatus status = await CheckAsync(handler, applyUrl);

        Assert.Equal(ApplyStatus.Active, status);
    }

    [Theory]
    [InlineData("mailto:jobs@example.test")]
    [InlineData("not a url")]
    public async Task ShouldReturnSkipped_WhenLinkIsNotWebAddress(string url) {
        FakeSourceHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        ApplyStatus status = await CheckAsync(handler, url);

        Assert.Equal(ApplyStatus.Skipped, status);
    }

    [Fact]
    public async Task ShouldReturnSkipped_WhenFinnLinkIsNotApplyPage() {
        FakeSourceHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        ApplyStatus status = await CheckAsync(handler, "https://www.finn.no/job/fulltime/ad.html?finnkode=1");

        Assert.Equal(ApplyStatus.Skipped, status);
    }

    [Fact]
    public async Task ShouldReturnExpired_WhenFinnApplyPageIsGone() {
        FakeSourceHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        ApplyStatus status = await CheckAsync(handler, "https://www.finn.no/job-apply/1");

        Assert.Equal(ApplyStatus.Expired, status);
    }

    [Fact]
    public async Task ShouldReturnUnknown_WhenBodyPassesCap() {
        FakeSourceHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StreamContent(new LargeBodyStream(hostileBodyBytes)),
        });

        ApplyStatus status = await CheckAsync(handler, applyUrl);

        Assert.Equal(ApplyStatus.Unknown, status);
    }

    [Fact]
    public async Task ShouldReturnUnknown_WhenPageDoesNotAnswer() {
        FakeSourceHandler handler = new(_ => throw new HttpRequestException(HttpRequestError.ConnectionError));

        ApplyStatus status = await CheckAsync(handler, applyUrl);

        Assert.Equal(ApplyStatus.Unknown, status);
    }

    [Fact]
    public async Task ShouldReturnUnknown_WhenRequestThrowsIOException() {
        FakeSourceHandler handler = new(_ => throw new IOException("connection reset"));

        ApplyStatus status = await CheckAsync(handler, applyUrl);

        Assert.Equal(ApplyStatus.Unknown, status);
    }

    [Fact]
    public async Task ShouldReturnUnknown_WhenConnectTimesOut() {
        FakeSourceHandler handler = new(_ => throw HttpFixtures.ConnectTimeout());

        ApplyStatus status = await CheckAsync(handler, applyUrl);

        Assert.Equal(ApplyStatus.Unknown, status);
    }

    [Theory]
    [InlineData(gzipEncoding)]
    [InlineData(brotliEncoding)]
    public async Task ShouldReturnUnknown_WhenBodyEncodingIsCorrupt(string encoding) {
        FakeSourceHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StreamContent(CorruptBody(encoding)),
        });

        ApplyStatus status = await CheckAsync(handler, applyUrl);

        Assert.Equal(ApplyStatus.Unknown, status);
    }

    [Fact]
    public async Task ShouldThrow_WhenCallerCancels() {
        FakeSourceHandler handler = new(_ => Page(activePage));
        using HttpClient client = new(handler);
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ApplyLinks.CheckAsync(client, applyUrl, TimeProvider.System, cancel.Token));
    }

    [Fact]
    public async Task ShouldReturnUnknown_WhenPageStallsPastDeadline() {
        FakeTimeProvider time = new();

        ((bool Early, bool OnTime) Timing, ApplyStatus Status) result = await CheckUntilDeadlineAsync(time, OpenBody);

        Assert.Equal(((false, true), ApplyStatus.Unknown), result);
    }

    [Fact]
    public async Task ShouldReturnUnknown_WhenBodyDripsPastDeadline() {
        FakeTimeProvider time = new();

        ((bool Early, bool OnTime) Timing, ApplyStatus Status) result =
            await CheckUntilDeadlineAsync(time, () => DripBody(time));

        Assert.Equal(((false, true), ApplyStatus.Unknown), result);
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldReturnUnknownWithoutWaiting_WhenPageAsksToRetryLater() {
        FakeTimeProvider time = new();
        FakeSourceHandler inner = new(_ => Unavailable());
        using HttpClient client = new(new HostLimitHandler(new HostLimiter(time), time) { InnerHandler = inner });

        ApplyStatus status = await ApplyLinks.CheckAsync(client, applyUrl, time, TestContext.Current.CancellationToken)
            .WaitAsync(settle, TestContext.Current.CancellationToken);

        Assert.Equal(ApplyStatus.Unknown, status);
    }

    [Fact]
    public async Task ShouldCheckNextAd_WhenApplyLinkRedirectsToDataUrl() {
        using TestDatabase db = new();
        AdsEntry ads = new(db.Connection, Clock());
        ads.Upsert(Numbered(1) with { ApplyUrl = applyUrl });
        ads.Upsert(Numbered(2) with { ApplyUrl = otherApplyUrl });
        FakeSourceHandler handler = new((_, url) => url.AbsoluteUri == applyUrl
            ? HttpFixtures.RawRedirect(dataUrl)
            : Page(activePage));
        using HttpClient client = new(handler);

        await ads.CheckApplyLinksAsync(client, Now, AllTargets, TestContext.Current.CancellationToken);

        Assert.Equal(
            ["finn:1|blocked|2026-10-09T10:00:00+00:00", "finn:2|active|2026-10-09T10:00:00+00:00"],
            TestDatabase.Rows(db.Connection, statusQuery));
    }

    [Fact]
    public async Task ShouldStoreUnknown_WhenCheckThrowsUnexpectedError() {
        using TestDatabase db = new();
        AdsEntry ads = new(db.Connection, Clock());
        ads.Upsert(Numbered(1) with { ApplyUrl = applyUrl });
        ads.Upsert(Numbered(2) with { ApplyUrl = otherApplyUrl });
        FakeSourceHandler handler = new((_, url) => url.AbsoluteUri == applyUrl
            ? throw new NotSupportedException("unexpected handler failure")
            : Page(activePage));
        using HttpClient client = new(handler);

        await ads.CheckApplyLinksAsync(client, Now, AllTargets, TestContext.Current.CancellationToken);

        Assert.Equal(
            ["finn:1|unknown|2026-10-09T10:00:00+00:00", "finn:2|active|2026-10-09T10:00:00+00:00"],
            TestDatabase.Rows(db.Connection, statusQuery));
    }

    [Fact]
    public async Task ShouldStopChecks_WhenCallerCancels() {
        using TestDatabase db = new();
        AdsEntry ads = new(db.Connection, Clock());
        ads.Upsert(Numbered(1) with { ApplyUrl = applyUrl });
        FakeSourceHandler handler = new(_ => Page(activePage));
        using HttpClient client = new(handler);
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ads.CheckApplyLinksAsync(client, Now, AllTargets, cancel.Token));
    }

    [Fact]
    public async Task ShouldRecheckOnlyStaleLinks_WhenLinksWereCheckedBefore() {
        using TestDatabase db = new();
        AdsEntry ads = new(db.Connection, Clock());
        AdStore store = new(db.Connection, Clock());
        ads.Upsert(Numbered(1) with { ApplyUrl = applyUrl });
        ads.Upsert(Numbered(2) with { ApplyUrl = otherApplyUrl });
        store.SetApplyStatus("finn:1", "active", Now.AddDays(-6));
        store.SetApplyStatus("finn:2", "active", Now.AddDays(-8));
        FakeSourceHandler handler = new(_ => Page(activePage));
        using HttpClient client = new(handler);

        await ads.CheckApplyLinksAsync(client, Now, AllTargets, TestContext.Current.CancellationToken);

        Assert.Equal([new Uri(otherApplyUrl)], handler.Urls);
    }

    [Fact]
    public async Task ShouldCheckSixtyLinks_WhenMoreArePending() {
        using TestDatabase db = new();
        AdsEntry ads = new(db.Connection, Clock());
        foreach (int index in Enumerable.Range(1, applyCheckLimit + 1)) {
            ads.Upsert(Numbered(index) with { ApplyUrl = applyUrl + "?n=" + index });
        }
        FakeSourceHandler handler = new(_ => Page(activePage));
        using HttpClient client = new(handler);

        await ads.CheckApplyLinksAsync(client, Now, AllTargets, TestContext.Current.CancellationToken);

        Assert.Equal(applyCheckLimit, handler.Urls.Count);
    }

    private static async Task<ApplyStatus> CheckAsync(HttpMessageHandler handler, string url) {
        using HttpClient client = new(handler);
        return await ApplyLinks.CheckAsync(client, url, TimeProvider.System, TestContext.Current.CancellationToken);
    }

    private static async Task<((bool Early, bool OnTime) Timing, ApplyStatus Status)> CheckUntilDeadlineAsync(
        FakeTimeProvider time, Func<HttpResponseMessage> answer) {
        using SemaphoreSlim requested = new(0);
        FakeSourceHandler handler = new(_ => {
            requested.Release();
            return answer();
        });
        using HttpClient client = new(handler);
        Task<ApplyStatus> check = ApplyLinks.CheckAsync(client, applyUrl, time, TestContext.Current.CancellationToken);
        await requested.WaitAsync(TestContext.Current.CancellationToken);

        (bool Early, bool OnTime) timing = await LimiterTiming.StateAroundAsync(time, check, checkDeadline);
        return (timing, await check);
    }

    private static HttpResponseMessage OpenBody() =>
        new(HttpStatusCode.OK) { Content = new StreamContent(new Pipe().Reader.AsStream()) };

    private static HttpResponseMessage DripBody(TimeProvider time) {
        Pipe pipe = new();
        _ = DripAsync(pipe.Writer, time);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(pipe.Reader.AsStream()) };
    }

    private static async Task DripAsync(PipeWriter writer, TimeProvider time) {
        while (true) {
            await Task.Delay(dripGap, time);
            await writer.WriteAsync(new byte[] { 0 });
        }
    }

    private static HttpResponseMessage Unavailable() {
        HttpResponseMessage response = new(HttpStatusCode.ServiceUnavailable);
        response.Headers.Add(retryAfterHeader, longRetryAfter);
        return response;
    }

    private static HttpResponseMessage Page(string html) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(html)) };

    private static Stream CorruptBody(string encoding) {
        MemoryStream compressed = new(Enumerable.Repeat(corruptByte, corruptBodyBytes).ToArray());
        return encoding == gzipEncoding
            ? new GZipStream(compressed, CompressionMode.Decompress)
            : new BrotliStream(compressed, CompressionMode.Decompress);
    }

}
