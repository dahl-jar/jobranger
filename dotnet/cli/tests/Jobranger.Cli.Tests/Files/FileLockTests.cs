using System.Runtime.Versioning;
using Jobranger.Cli.Files;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobranger.Cli.Tests.Files;

[UnsupportedOSPlatform("windows")]
public sealed class FileLockTests : IDisposable {
    private static readonly TimeSpan shortWait = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan longWait = TimeSpan.FromSeconds(10);
    private static readonly ILogger logger = NullLogger.Instance;

    private readonly TempDir temp = new();

    public void Dispose() => temp.Dispose();

    [Fact]
    public void ShouldReturnNull_WhenLockIsHeld() {
        string path = temp.File("run.lock");
        using FileLock? holder = FileLock.TryAcquire(path, logger);

        using FileLock? second = FileLock.TryAcquire(path, logger);

        Assert.Null(second);
    }

    [Fact]
    public void ShouldCreatePrivateLockFile_WhenDirIsMissing() {
        string path = Path.Combine(temp.Path, "state", "run.lock");

        using FileLock? held = FileLock.TryAcquire(path, logger);

        UnixFileMode[] modes = [File.GetUnixFileMode(Path.GetDirectoryName(path)!), File.GetUnixFileMode(path)];
        Assert.Equal([FileModes.OwnerOnlyDir, FileModes.OwnerReadWrite], modes);
    }

    [Fact]
    public void ShouldRefuseLock_WhenPathIsSymlink() {
        using TempDir outside = new();
        string target = outside.File("target");
        File.WriteAllText(target, "kept");
        string path = temp.File("run.lock");
        File.CreateSymbolicLink(path, target);

        PrivateFileException refused =
            Assert.Throws<PrivateFileException>(() => FileLock.TryAcquire(path, logger));

        Assert.Equal("symbolic link", refused.Reason);
    }

    [Fact]
    public void ShouldRefuseLock_WhenPathIsDirectory() {
        string path = temp.File("run.lock");
        Directory.CreateDirectory(path);

        Assert.Throws<PrivateFileException>(() => FileLock.TryAcquire(path, logger));
    }

    [Fact]
    public void ShouldAcquire_WhenHolderReleasesWithinWait() {
        string path = temp.File("secrets.json.lock");
        FileLock holder = FileLock.TryAcquire(path, logger)!;
        using Timer release = new(_ => holder.Dispose(), null, shortWait, Timeout.InfiniteTimeSpan);

        using FileLock acquired = FileLock.Acquire(path, longWait, logger, TestContext.Current.CancellationToken);

        Assert.Null(FileLock.TryAcquire(path, logger));
    }

    [Fact(Timeout = 5000)]
    public async Task ShouldThrowTimeout_WhenLockStaysHeld() {
        string path = temp.File("secrets.json.lock");
        using FileLock? holder = FileLock.TryAcquire(path, logger);

        await Assert.ThrowsAsync<TimeoutException>(() => Task.Run(
            () => FileLock.Acquire(path, shortWait, logger, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldStopWaiting_WhenTokenIsCancelled() {
        string path = temp.File("secrets.json.lock");
        using FileLock? holder = FileLock.TryAcquire(path, logger);
        using CancellationTokenSource cancel = new(shortWait);

        Assert.ThrowsAny<OperationCanceledException>(() => FileLock.Acquire(path, longWait, logger, cancel.Token));
    }
}
