using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace Jobranger.Cli.Files;

[UnsupportedOSPlatform("windows")]
internal sealed class FileLock : IDisposable {
    private const string lockFailedReason = "lock failed errno=";

    private static readonly TimeSpan retryInterval = TimeSpan.FromMilliseconds(50);

    private readonly SafeFileHandle handle;

    private FileLock(SafeFileHandle handle) {
        this.handle = handle;
    }

    public static FileLock? TryAcquire(string path, ILogger logger) {
        PrivateFile.CreateParentDir(path, logger);
        CreateIfMissing(path);
        SafeFileHandle handle = UnixFiles.OpenLockNoFollow(path, out int openError)
            ?? throw PrivateFile.OpenError(path, openError);
        if (UnixFiles.TryLockExclusive(handle, out int lockError)) {
            return new FileLock(handle);
        }
        handle.Dispose();
        return UnixFiles.IsWouldBlock(lockError)
            ? null
            : throw new PrivateFileException(path, lockFailedReason + lockError);
    }

    public static FileLock Acquire(string path, TimeSpan wait, ILogger logger, CancellationToken cancellationToken) {
        long started = Stopwatch.GetTimestamp();
        while (true) {
            if (TryAcquire(path, logger) is FileLock acquired) {
                return acquired;
            }
            if (Stopwatch.GetElapsedTime(started) >= wait) {
                throw new TimeoutException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"lock still held after {wait.TotalSeconds}s file={Path.GetFileName(path)}"));
            }
            cancellationToken.WaitHandle.WaitOne(retryInterval);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public void Dispose() => handle.Dispose();

    private static void CreateIfMissing(string path) {
        if (!UnixFiles.TryCreateNew(path, PrivateFile.OwnerOnlyFileMode, out int error) && !UnixFiles.IsExisting(error)) {
            throw PrivateFile.OpenError(path, error);
        }
    }
}
