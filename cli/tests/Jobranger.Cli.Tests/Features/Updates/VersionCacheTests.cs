using System.Net;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Updates;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Updates;

[UnsupportedOSPlatform("windows")]
public sealed class VersionCacheTests : IDisposable {
    private const UnixFileMode readOnlyDir = UnixFileMode.UserRead | UnixFileMode.UserExecute;

    private static readonly DateTimeOffset now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly Version highest = Version.Parse(GitHubReleases.Highest);

    private readonly TempDir temp = new();
    private readonly string cache;

    public VersionCacheTests() {
        cache = temp.File("version_check.json");
    }

    public static TheoryData<TimeSpan> FreshAges => new() { TimeSpan.Zero, TimeSpan.FromDays(1) - TimeSpan.FromSeconds(1) };

    public static TheoryData<TimeSpan> StaleAges => new() { TimeSpan.FromDays(1), TimeSpan.FromSeconds(-1) };

    public static TheoryData<byte[]> CorruptCaches => new() {
        "{not json"u8.ToArray(),
        "[]"u8.ToArray(),
        "{\"checked_at\":\"2026-10-12T09:00:00\",\"latest\":\"1.0.0\"}"u8.ToArray(),
        "{\"checked_at\":\"2026-10-12T09:00:00.0000000+00:00\",\"latest\":\"not a version\"}"u8.ToArray(),
        Utf8Fixtures.WithInvalidByte("{\"checked_at\":\"2026-10-12T09:00:00", "\",\"latest\":\"1.0.0\"}"),
    };

    public void Dispose() {
        File.SetUnixFileMode(temp.Path, FileModes.OwnerOnlyDir);
        temp.Dispose();
    }

    [Theory]
    [MemberData(nameof(FreshAges))]
    public async Task ShouldUseCache_WhenCheckedWithinOneDay(TimeSpan age) {
        await LatestAsync(new FakeGitHubHandler(GitHubReleases.All()), now);
        FakeGitHubHandler second = new(GitHubReleases.Only("cli-v11.0.0"));

        Version? latest = await LatestAsync(second, now + age);

        Assert.Equal((highest, 0), (latest, second.RequestCount));
    }

    [Theory]
    [MemberData(nameof(StaleAges))]
    public async Task ShouldQueryGitHub_WhenCacheIsStale(TimeSpan age) {
        await LatestAsync(new FakeGitHubHandler(GitHubReleases.All()), now);

        Version? latest = await LatestAsync(new FakeGitHubHandler(GitHubReleases.Only("cli-v11.0.0")), now + age);

        Assert.Equal(new Version(11, 0, 0), latest);
    }

    [Fact]
    public async Task ShouldSkipQuery_WhenLastCheckFailedWithinOneDay() {
        await LatestAsync(new FakeGitHubHandler(HttpStatusCode.InternalServerError, []), now);
        FakeGitHubHandler second = new(GitHubReleases.All());

        await LatestAsync(second, now + TimeSpan.FromHours(1));

        Assert.Equal(0, second.RequestCount);
    }

    [Theory]
    [MemberData(nameof(CorruptCaches))]
    public async Task ShouldQueryGitHub_WhenCacheIsCorrupt(byte[] content) {
        File.WriteAllBytes(cache, content);

        Version? latest = await LatestAsync(new FakeGitHubHandler(GitHubReleases.All()), now);

        Assert.Equal(highest, latest);
    }

    [Fact]
    public async Task ShouldQueryGitHub_WhenCacheIsSymlink() {
        File.CreateSymbolicLink(cache, temp.File("elsewhere.json"));

        Version? latest = await LatestAsync(new FakeGitHubHandler(GitHubReleases.All()), now);

        Assert.Equal(highest, latest);
    }

    [Fact]
    public async Task ShouldReturnLatest_WhenCacheCannotBeWritten() {
        File.SetUnixFileMode(temp.Path, readOnlyDir);

        Version? latest = await LatestAsync(new FakeGitHubHandler(GitHubReleases.All()), now);

        Assert.Equal(highest, latest);
    }

    private async Task<Version?> LatestAsync(HttpMessageHandler handler, DateTimeOffset at) {
        using HttpClient client = new(handler);
        return await VersionCheck.LatestAsync(client, cache, at, TestContext.Current.CancellationToken);
    }
}
