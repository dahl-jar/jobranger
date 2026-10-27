using System.Net;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Updates;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Updates;

[UnsupportedOSPlatform("windows")]
public sealed class VersionCheckTests : IDisposable {
    private const int maxBodyBytes = 8 * 1024 * 1024;
    private const int testTimeoutMs = 10_000;

    private static readonly DateTimeOffset now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly Version highest = Version.Parse(GitHubReleases.Highest);

    private readonly TempDir temp = new();
    private readonly string cache;

    public VersionCheckTests() {
        cache = temp.File("version_check.json");
    }

    public static TheoryData<HttpStatusCode, byte[]> UnusableReplies => new() {
        { HttpStatusCode.InternalServerError, "[{\"tag_name\":\"cli-v10.0.0\"}]"u8.ToArray() },
        { HttpStatusCode.OK, "{not json"u8.ToArray() },
        { HttpStatusCode.OK, "{\"tag_name\":\"cli-v10.0.0\"}"u8.ToArray() },
        { HttpStatusCode.OK, "[{\"tag_name\":10}]"u8.ToArray() },
        { HttpStatusCode.OK, Utf8Fixtures.WithInvalidByte("[{\"tag_name\":\"cli-v", "\"}]") },
        { HttpStatusCode.OK, "[{\"tag_name\":\"cli-v\\ud800\"}]"u8.ToArray() },
    };

    public void Dispose() => temp.Dispose();

    [Fact]
    public async Task ShouldPickHighestStableCliRelease_WhenReleasesAreListed() {
        Version? latest = await LatestAsync(new FakeGitHubHandler(GitHubReleases.All()), now);

        Assert.Equal(highest, latest);
    }

    [Theory]
    [MemberData(nameof(UnusableReplies))]
    public async Task ShouldReturnNull_WhenReplyIsUnusable(HttpStatusCode status, byte[] body) {
        Version? latest = await LatestAsync(new FakeGitHubHandler(status, body), now);

        Assert.Null(latest);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task ShouldReadReplyUpToSizeCap_WhenBodyIsLarge(int extra, bool found) {
        FakeGitHubHandler handler = new(GitHubReleases.StableOfSize(maxBodyBytes + extra));

        Version? latest = await LatestAsync(handler, now);

        Assert.Equal(found ? highest : null, latest);
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldGiveUpVersionCheck_WhenServerStallsPastTwoSeconds() {
        using HttpClient client = new(new StalledBodyHandler("[{\"tag_name\":"u8.ToArray()));

        Version? latest = await VersionCheck.LatestAsync(client, cache, now, TestContext.Current.CancellationToken);

        Assert.Null(latest);
    }

    [Fact]
    public async Task ShouldThrowCancelled_WhenCallerCancels() {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        using HttpClient client = new(new FakeGitHubHandler(GitHubReleases.All()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => VersionCheck.LatestAsync(client, cache, now, cancelled.Token));
    }

    private async Task<Version?> LatestAsync(HttpMessageHandler handler, DateTimeOffset at) {
        using HttpClient client = new(handler);
        return await VersionCheck.LatestAsync(client, cache, at, TestContext.Current.CancellationToken);
    }
}
