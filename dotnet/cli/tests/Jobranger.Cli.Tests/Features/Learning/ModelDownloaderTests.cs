using System.Security.Cryptography;
using Jobranger.Cli.Features.Learning;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Features.Learning;

public sealed class ModelDownloaderTests : IDisposable {
    private static readonly Uri url = new("https://models.example.test/model.onnx");
    private static readonly byte[] body = [1, 2, 3, 4];
    private static readonly ModelFile file =
        new("model.onnx", url, Convert.ToHexStringLower(SHA256.HashData(body)), body.Length);

    private static readonly TimeSpan idleTimeout = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan staleAge = TimeSpan.FromHours(1);
    private static readonly TimeSpan trickleIdleTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan trickleGap = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan longIdleTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan cancelAfter = TimeSpan.FromMilliseconds(100);
    private const int testTimeoutMs = 10_000;

    private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("jobranger-models-");

    public void Dispose() => folder.Delete(recursive: true);

    [Fact]
    public async Task ShouldSaveFile_WhenHashMatches() {
        using HttpClient http = Serving(body);
        ModelDownloader downloader = Downloader(http);

        await downloader.EnsureAsync(TestContext.Current.CancellationToken);

        Assert.Equal(body, File.ReadAllBytes(Path.Combine(folder.FullName, file.Name)));
    }

    [Fact]
    public async Task ShouldCreateModelFolder_WhenFolderIsMissing() {
        string models = Path.Combine(folder.FullName, "models");
        using HttpClient http = Serving(body);
        ModelDownloader downloader = new(http, models, [file]);

        await downloader.EnsureAsync(TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(models, file.Name)));
    }

    [Fact]
    public async Task ShouldDeleteDownload_WhenHashDiffers() {
        byte[] other = [4, 3, 2, 1];
        using HttpClient http = Serving(other);
        ModelDownloader downloader = Downloader(http);

        await Assert.ThrowsAsync<ModelHashException>(
            () => downloader.EnsureAsync(TestContext.Current.CancellationToken));

        Assert.Empty(folder.GetFiles());
    }

    [Fact]
    public async Task ShouldStopDownload_WhenBodyExceedsPinnedSize() {
        byte[] larger = [.. body, 5];
        using HttpClient http = Serving(larger);
        ModelDownloader downloader = Downloader(http);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => downloader.EnsureAsync(TestContext.Current.CancellationToken));

        Assert.Empty(folder.GetFiles());
    }

    [Fact]
    public async Task ShouldThrowHttpError_WhenServerAnswersNotFound() {
        using HttpClient http = new(new FakeHttpHandler(new Dictionary<Uri, byte[]>()));
        ModelDownloader downloader = Downloader(http);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => downloader.EnsureAsync(TestContext.Current.CancellationToken));

        Assert.Empty(folder.GetFiles());
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldFailDownload_WhenBodyStalls() {
        RecordingTime time = new();
        using HttpClient http = new(new StalledBodyHandler([1, 2]));
        ModelDownloader downloader = new(http, folder.FullName, [file], longIdleTimeout, time);
        Task download = downloader.EnsureAsync(TestContext.Current.CancellationToken);
        await time.WaitForAsync(longIdleTimeout);

        time.Advance(longIdleTimeout);

        await Assert.ThrowsAsync<ModelDownloadException>(() => download);
        Assert.Empty(folder.GetFiles());
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldFinishDownload_WhenChunksArriveWithinIdleTimeout() {
        RecordingTime time = new();
        using HttpClient http = new(new TrickleBodyHandler(body, trickleGap, time));
        ModelDownloader downloader = new(http, folder.FullName, [file], trickleIdleTimeout, time);
        Task download = downloader.EnsureAsync(TestContext.Current.CancellationToken);

        for (int chunk = 0; chunk < body.Length; chunk++) {
            await time.WaitForAsync(trickleGap);
            time.Advance(trickleGap);
        }

        await download;
        Assert.Equal(body, File.ReadAllBytes(Path.Combine(folder.FullName, file.Name)));
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldPropagateCancellation_WhenCallerCancels() {
        using HttpClient http = new(new StalledBodyHandler([1, 2]));
        ModelDownloader downloader = new(http, folder.FullName, [file], longIdleTimeout, new FakeTimeProvider());
        using CancellationTokenSource caller =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        caller.CancelAfter(cancelAfter);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.EnsureAsync(caller.Token));

        Assert.Empty(folder.GetFiles());
    }

    [Fact]
    public async Task ShouldDeleteStalePartFiles_WhenDownloadStarts() {
        string stale = Path.Combine(folder.FullName, file.Name + ".0123abcd.part");
        File.WriteAllBytes(stale, body);
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow - staleAge);
        using HttpClient http = Serving(body);
        ModelDownloader downloader = Downloader(http);

        await downloader.EnsureAsync(TestContext.Current.CancellationToken);

        Assert.False(File.Exists(stale));
    }

    [Fact]
    public async Task ShouldDeleteStalePartFiles_WhenModelIsPresent() {
        File.WriteAllBytes(Path.Combine(folder.FullName, file.Name), body);
        string stale = Path.Combine(folder.FullName, file.Name + ".0123abcd.part");
        File.WriteAllBytes(stale, body);
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow - staleAge);
        using HttpClient http = Serving(body);
        ModelDownloader downloader = Downloader(http);

        await downloader.EnsureAsync(TestContext.Current.CancellationToken);

        Assert.False(File.Exists(stale));
    }

    [Fact]
    public async Task ShouldKeepPartFile_WhenAnotherDownloadWroteItRecently() {
        string active = Path.Combine(folder.FullName, file.Name + ".0123abcd.part");
        File.WriteAllBytes(active, body);
        using HttpClient http = Serving(body);
        ModelDownloader downloader = Downloader(http);

        await downloader.EnsureAsync(TestContext.Current.CancellationToken);

        Assert.True(File.Exists(active));
    }

    [Fact]
    public async Task ShouldSkipDownload_WhenFileExists() {
        File.WriteAllBytes(Path.Combine(folder.FullName, file.Name), body);
        FakeHttpHandler handler = new(new Dictionary<Uri, byte[]> { [url] = body });
        using HttpClient http = new(handler);
        ModelDownloader downloader = Downloader(http);

        await downloader.EnsureAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, handler.RequestCount);
    }

    private static HttpClient Serving(byte[] content) =>
        new(new FakeHttpHandler(new Dictionary<Uri, byte[]> { [url] = content }));

    private ModelDownloader Downloader(HttpClient http) =>
        new(http, folder.FullName, [file], idleTimeout, new FakeTimeProvider());
}
